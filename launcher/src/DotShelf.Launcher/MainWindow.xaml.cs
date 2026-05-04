using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DotShelf.Launcher.Core;
using Microsoft.Win32;

namespace DotShelf.Launcher;

public partial class MainWindow : Window
{
    private const string DefaultManifestUrl = "https://dotshelf.digital/launcher/windows/latest.json";
    private const string DefaultEventsUrl = "https://dotshelf.digital/launcher/windows/events.json";
    private static readonly TimeSpan BannerRotationInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BannerManualPauseDuration = TimeSpan.FromSeconds(10);

    private readonly ManifestClient manifestClient = new();
    private readonly EventsClient eventsClient = new();
    private readonly LauncherSettings settings;
    private readonly DispatcherTimer gameTimer = new();
    private readonly DispatcherTimer bannerRotateTimer = new();
    private readonly DispatcherTimer bannerResumeTimer = new();
    private readonly Random gameRandom = new();
    private readonly List<FallingGameItem> fallingGameItems = new();
    private List<EventBanner> activeBanners = new();
    private int currentBannerIndex;
    private PackageInstaller packageInstaller;
    private LauncherManifest? latestManifest;
    private LauncherInstallState? installedState;
    private LauncherUiState currentUiState = LauncherUiState.Checking;
    private int lastLoggedProgressPercent = -1;
    private string lastLoggedProgressMessage = "";
    private DateTimeOffset lastLoggedProgressAt = DateTimeOffset.MinValue;
    private Border? gamePlayer;
    private DateTimeOffset lastGameTickAt = DateTimeOffset.MinValue;
    private double gameSpawnElapsed;
    private double nextGameSpawnDelay = 0.75;
    private double gamePlayerX;
    private int gameScore;
    private bool isGameRunning;
    private bool isMovingLeft;
    private bool isMovingRight;
    private bool isBusy;

    public MainWindow()
    {
        InitializeComponent();
        settings = LauncherSettings.Load();
        packageInstaller = new PackageInstaller(settings.CreatePaths());
        gameTimer.Interval = TimeSpan.FromMilliseconds(16);
        gameTimer.Tick += GameTimer_Tick;
        bannerRotateTimer.Interval = BannerRotationInterval;
        bannerRotateTimer.Tick += BannerRotateTimer_Tick;
        bannerResumeTimer.Interval = BannerManualPauseDuration;
        bannerResumeTimer.Tick += BannerResumeTimer_Tick;
        UpdateGameHud();
        ApplyUiState(LauncherUiState.Checking);
        Loaded += MainWindow_Loaded;
    }

    private enum LauncherUiState
    {
        Checking,
        NotInstalled,
        UpdateAvailable,
        Ready,
        OfflineReady,
        Error
    }

    private sealed class FallingGameItem
    {
        public required FrameworkElement Element { get; init; }
        public bool IsBook { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double Speed { get; init; }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log("런처 상태를 확인합니다.");
        await RefreshLauncherStateAsync();
        await RefreshBannersAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw when the mouse button is released during the call.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!isGameRunning)
        {
            return;
        }

        if (e.Key is Key.Left or Key.A)
        {
            isMovingLeft = true;
            e.Handled = true;
        }
        else if (e.Key is Key.Right or Key.D)
        {
            isMovingRight = true;
            e.Handled = true;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.A)
        {
            isMovingLeft = false;
            e.Handled = true;
        }
        else if (e.Key is Key.Right or Key.D)
        {
            isMovingRight = false;
            e.Handled = true;
        }
    }

    private void StartGameButton_Click(object sender, RoutedEventArgs e)
    {
        StartMiniGame();
    }

    private void RetryGameButton_Click(object sender, RoutedEventArgs e)
    {
        StartMiniGame();
    }

    private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        GameCanvas.Focus();
    }

    private void StartMiniGame()
    {
        gameTimer.Stop();
        fallingGameItems.Clear();
        GameCanvas.Children.Clear();

        gameScore = 0;
        isGameRunning = true;
        isMovingLeft = false;
        isMovingRight = false;
        gameSpawnElapsed = 0;
        nextGameSpawnDelay = 0.35;
        lastGameTickAt = DateTimeOffset.UtcNow;

        gamePlayer = CreateGamePlayer();
        GameCanvas.Children.Add(gamePlayer);
        MoveGamePlayer(GetGameWidth() / 2);
        Canvas.SetTop(gamePlayer, GetGameHeight() - 38);

        GameOverlay.Visibility = Visibility.Collapsed;
        StartGameButton.Visibility = Visibility.Collapsed;
        RetryGameButton.Visibility = Visibility.Collapsed;
        UpdateGameHud();
        GameOverlayTitle.Text = "하늘책 수집";
        GameOverlayDescription.Text = "책을 받으면 점수가 오르고, 어두운 장애물에 닿으면 게임이 끝납니다.";
        GameOverlayInstruction.Text = "← / → 방향키로 바구니를 움직이세요.";
        Keyboard.Focus(GameCanvas);
        gameTimer.Start();
    }

    private void GameTimer_Tick(object? sender, EventArgs e)
    {
        if (!isGameRunning)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var deltaSeconds = Math.Clamp((now - lastGameTickAt).TotalSeconds, 0.001, 0.04);
        lastGameTickAt = now;

        MoveGamePlayerByKeyboard(deltaSeconds);

        gameSpawnElapsed += deltaSeconds;
        if (gameSpawnElapsed >= nextGameSpawnDelay)
        {
            gameSpawnElapsed = 0;
            nextGameSpawnDelay = Math.Max(0.34, 0.72 - gameScore * 0.012) + gameRandom.NextDouble() * 0.22;
            SpawnGameItem();
        }

        MoveFallingGameItems(deltaSeconds);
    }

    private void SpawnGameItem()
    {
        var canvasWidth = GetGameWidth();
        var isBook = gameRandom.NextDouble() >= 0.28;
        var width = isBook ? gameRandom.Next(22, 32) : gameRandom.Next(28, 38);
        var height = isBook ? gameRandom.Next(34, 50) : gameRandom.Next(28, 38);
        var x = gameRandom.NextDouble() * Math.Max(1, canvasWidth - width - 12) + 6;
        var speed = 150 + gameScore * 4 + gameRandom.Next(0, 80);
        var item = new FallingGameItem
        {
            Element = isBook ? CreateGameBook(width, height) : CreateGameObstacle(width, height),
            IsBook = isBook,
            X = x,
            Y = -height - 4,
            Width = width,
            Height = height,
            Speed = speed
        };

        fallingGameItems.Add(item);
        GameCanvas.Children.Add(item.Element);
        Canvas.SetLeft(item.Element, item.X);
        Canvas.SetTop(item.Element, item.Y);
    }

    private void MoveFallingGameItems(double deltaSeconds)
    {
        var canvasHeight = GetGameHeight();
        var playerBounds = GetPlayerBounds();

        for (var index = fallingGameItems.Count - 1; index >= 0; index--)
        {
            var item = fallingGameItems[index];
            item.Y += item.Speed * deltaSeconds;
            Canvas.SetTop(item.Element, item.Y);

            var itemBounds = new Rect(item.X, item.Y, item.Width, item.Height);
            if (playerBounds.IntersectsWith(itemBounds))
            {
                if (item.IsBook)
                {
                    RemoveGameItem(index);
                    AddGameScore();
                    continue;
                }

                EndMiniGame();
                return;
            }

            if (item.Y > canvasHeight + 24)
            {
                RemoveGameItem(index);
            }
        }
    }

    private void AddGameScore()
    {
        gameScore++;
        if (gameScore > settings.BestGameScore)
        {
            settings.BestGameScore = gameScore;
            settings.Save();
        }

        UpdateGameHud();
    }

    private void EndMiniGame()
    {
        isGameRunning = false;
        gameTimer.Stop();
        GameOverlay.Visibility = Visibility.Visible;
        GameOverlayTitle.Text = "게임 오버";
        GameOverlayDescription.Text = $"점수 {gameScore}점 · 최고기록 {settings.BestGameScore}점";
        GameOverlayInstruction.Text = "";
        StartGameButton.Visibility = Visibility.Collapsed;
        RetryGameButton.Visibility = Visibility.Visible;
    }

    private void RemoveGameItem(int index)
    {
        var item = fallingGameItems[index];
        GameCanvas.Children.Remove(item.Element);
        fallingGameItems.RemoveAt(index);
    }

    private Border CreateGamePlayer()
    {
        return new Border
        {
            Width = 78,
            Height = 20,
            CornerRadius = new CornerRadius(5),
            Background = BrushFromHex("#3F4A3A"),
            BorderBrush = BrushFromHex("#F6F2EC"),
            BorderThickness = new Thickness(1)
        };
    }

    private FrameworkElement CreateGameBook(double width, double height)
    {
        var colors = new[] { "#7A4A36", "#A38069", "#3F4A3A", "#C2A07A", "#8E6E5A" };
        var grid = new Grid
        {
            Width = width,
            Height = height
        };

        grid.Children.Add(new Border
        {
            Background = BrushFromHex(colors[gameRandom.Next(colors.Length)]),
            BorderBrush = BrushFromHex("#F6F2EC"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3)
        });
        grid.Children.Add(new Border
        {
            Width = 3,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(5, 5, 0, 5),
            Background = BrushFromHex("#F1DFC6"),
            CornerRadius = new CornerRadius(2)
        });

        return grid;
    }

    private FrameworkElement CreateGameObstacle(double width, double height)
    {
        var shapeSize = Math.Max(width, height);
        var grid = new Grid
        {
            Width = width,
            Height = height
        };

        grid.Children.Add(new System.Windows.Shapes.Polygon
        {
            Points = new PointCollection
            {
                new(width * 0.50, 0),
                new(width * 0.63, height * 0.25),
                new(width, height * 0.18),
                new(width * 0.76, height * 0.50),
                new(width, height * 0.82),
                new(width * 0.63, height * 0.75),
                new(width * 0.50, height),
                new(width * 0.37, height * 0.75),
                new(0, height * 0.82),
                new(width * 0.24, height * 0.50),
                new(0, height * 0.18),
                new(width * 0.37, height * 0.25)
            },
            Fill = BrushFromHex("#5A1F1F"),
            Stroke = BrushFromHex("#F0C66A"),
            StrokeThickness = 1.4
        });

        grid.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = width * 0.55,
            Height = height * 0.55,
            Fill = BrushFromHex("#2E251F"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        grid.Children.Add(new TextBlock
        {
            Text = "!",
            Foreground = BrushFromHex("#F6DFA8"),
            FontWeight = FontWeights.Black,
            FontSize = Math.Max(16, shapeSize * 0.62),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0)
        });

        return grid;
    }

    private void MoveGamePlayer(double centerX)
    {
        if (gamePlayer == null)
        {
            return;
        }

        var canvasWidth = GetGameWidth();
        gamePlayerX = Math.Clamp(centerX - gamePlayer.Width / 2, 6, Math.Max(6, canvasWidth - gamePlayer.Width - 6));
        Canvas.SetLeft(gamePlayer, gamePlayerX);
        Canvas.SetTop(gamePlayer, GetGameHeight() - 38);
    }

    private void MoveGamePlayerByKeyboard(double deltaSeconds)
    {
        if (gamePlayer == null || isMovingLeft == isMovingRight)
        {
            return;
        }

        var direction = isMovingLeft ? -1 : 1;
        var playerCenter = gamePlayerX + gamePlayer.Width / 2;
        MoveGamePlayer(playerCenter + direction * 300 * deltaSeconds);
    }

    private Rect GetPlayerBounds()
    {
        if (gamePlayer == null)
        {
            return Rect.Empty;
        }

        return new Rect(gamePlayerX, GetGameHeight() - 38, gamePlayer.Width, gamePlayer.Height);
    }

    private double GetGameWidth()
    {
        return Math.Max(280, GameCanvas.ActualWidth);
    }

    private double GetGameHeight()
    {
        return Math.Max(320, GameCanvas.ActualHeight);
    }

    private void UpdateGameHud()
    {
        GameScoreText.Text = gameScore.ToString();
        GameBestScoreText.Text = settings.BestGameScore.ToString();
    }

    private async void ChooseInstallLocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "DotShelf 설치 위치 선택",
            InitialDirectory = Directory.Exists(settings.InstallRoot)
                ? settings.InstallRoot
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        settings.InstallRoot = Path.GetFullPath(dialog.FolderName);
        settings.Save();
        packageInstaller = new PackageInstaller(settings.CreatePaths());
        Log($"설치 기준 위치를 변경했습니다: {settings.InstallRoot}");
        await RefreshLauncherStateAsync();
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        var completed = await RunActionAsync(async cancellationToken =>
        {
            var manifest = await EnsureLatestManifestAsync(cancellationToken);
            var actionName = packageInstaller.HasInstalledExecutable(installedState) ? "업데이트" : "설치";

            Log($"버전 {manifest.LatestVersion} {actionName}를 시작합니다.");
            ResetInstallProgressLog();
            var progress = new Progress<InstallProgress>(value =>
            {
                ProgressBar.Value = value.Percent;
                StatusText.Text = $"{value.Message} {value.Percent:0}%";
                LogInstallProgress(value);
            });

            var state = await packageInstaller.InstallOrUpdateAsync(manifest, progress, cancellationToken);
            installedState = state;
            VersionText.Text = $"설치됨 {state.InstalledVersion}";
            UpdateInstallPathText(state, true);
            StatusText.Text = $"{actionName}가 완료되었습니다.";
            Log($"설치 위치: {state.InstallPath}");
        });

        if (completed)
        {
            await RefreshLauncherStateAsync(resetProgress: false);
            ProgressBar.Value = 100;
        }
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync(async cancellationToken =>
        {
            var process = await packageInstaller.LaunchAsync(cancellationToken);
            StatusText.Text = "DotShelf 앱을 실행했습니다.";
            Log($"실행된 프로세스: {process.Id}");
        });
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "설치된 DotShelf 앱을 삭제할까요?",
            "설치 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var completed = await RunActionAsync(async cancellationToken =>
        {
            Log("설치된 앱을 삭제합니다.");
            await packageInstaller.DeleteInstalledAppAsync(cancellationToken);
            installedState = null;
            ProgressBar.Value = 0;
            Log("설치 파일을 삭제했습니다.");
        });

        if (completed)
        {
            await RefreshLauncherStateAsync();
        }
    }

    private async Task RefreshLauncherStateAsync(bool resetProgress = true)
    {
        SetBusy(true);
        ApplyUiState(LauncherUiState.Checking);
        latestManifest = null;

        try
        {
            if (resetProgress)
            {
                ProgressBar.Value = 0;
            }

            PackageSizeText.Text = "설치 파일 크기 확인 중";
            installedState = await packageInstaller.GetInstallStateAsync();
            var hasLocalExecutable = packageInstaller.HasInstalledExecutable(installedState);

            if (hasLocalExecutable && installedState != null)
            {
                VersionText.Text = $"설치됨 {installedState.InstalledVersion}";
                UpdateInstallPathText(installedState, true);
            }
            else
            {
                VersionText.Text = "설치되지 않음";
                UpdateInstallPathText(null, false);
            }

            var manifest = await EnsureLatestManifestAsync(CancellationToken.None);
            await UpdatePackageSizeTextAsync(manifest, CancellationToken.None);
            if (!hasLocalExecutable || installedState == null)
            {
                StatusText.Text = $"버전 {manifest.LatestVersion}을 설치할 수 있습니다.";
                ApplyUiState(LauncherUiState.NotInstalled);
                return;
            }

            var isSameVersion = ReleaseVersionComparer.Compare(installedState.InstalledVersion, manifest.LatestVersion) == 0;
            if (isSameVersion)
            {
                VersionText.Text = $"최신 버전 {installedState.InstalledVersion}";
                StatusText.Text = "시작할 준비가 되었습니다.";
                ApplyUiState(LauncherUiState.Ready);
                return;
            }

            StatusText.Text = $"설치됨 {installedState.InstalledVersion} · 최신 빌드 {manifest.LatestVersion}";
            ApplyUiState(LauncherUiState.UpdateAvailable);
        }
        catch (Exception exception)
        {
            Log($"최신 빌드 정보를 불러오지 못했습니다: {exception.Message}");

            installedState ??= await packageInstaller.GetInstallStateAsync();
            if (packageInstaller.HasInstalledExecutable(installedState) && installedState != null)
            {
                VersionText.Text = $"설치됨 {installedState.InstalledVersion}";
                UpdateInstallPathText(installedState, true);
                PackageSizeText.Text = "설치 파일 크기: 확인 불가";
                StatusText.Text = "온라인 버전 확인에 실패했습니다. 설치된 앱은 실행할 수 있습니다.";
                ApplyUiState(LauncherUiState.OfflineReady);
                return;
            }

            VersionText.Text = "설치되지 않음";
            UpdateInstallPathText(null, false);
            PackageSizeText.Text = "설치 파일 크기: 확인 불가";
            StatusText.Text = "최신 빌드 정보를 불러와야 설치할 수 있습니다.";
            ApplyUiState(LauncherUiState.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<LauncherManifest> EnsureLatestManifestAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(DefaultManifestUrl, UriKind.Absolute, out var manifestUri))
        {
            throw new InvalidOperationException("업데이트 주소가 올바르지 않습니다.");
        }

        latestManifest = await manifestClient.GetLatestAsync(manifestUri, cancellationToken);
        return latestManifest;
    }

    private async Task UpdatePackageSizeTextAsync(LauncherManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            var packageSize = await manifestClient.GetPackageSizeAsync(new Uri(manifest.PackageUrl), cancellationToken);
            PackageSizeText.Text = packageSize.HasValue
                ? $"설치 파일 크기: {FormatPackageSize(packageSize.Value)}"
                : "설치 파일 크기: 확인 불가";
        }
        catch (Exception exception)
        {
            PackageSizeText.Text = "설치 파일 크기: 확인 불가";
            Log($"설치 파일 크기를 확인하지 못했습니다: {exception.Message}");
        }
    }

    private async Task<bool> RunActionAsync(Func<CancellationToken, Task> action)
    {
        SetBusy(true);
        try
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            await action(cancellationTokenSource.Token);
            return true;
        }
        catch (Exception exception)
        {
            StatusText.Text = "작업을 완료하지 못했습니다.";
            Log(exception.Message);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        ChooseInstallLocationButton.IsEnabled = !busy;
        ApplyUiState(currentUiState);
    }

    private void ApplyUiState(LauncherUiState uiState)
    {
        currentUiState = uiState;

        ActionButton.Visibility = uiState is LauncherUiState.NotInstalled or LauncherUiState.UpdateAvailable
            ? Visibility.Visible
            : Visibility.Collapsed;
        LaunchButton.Visibility = uiState is LauncherUiState.Ready or LauncherUiState.OfflineReady
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeleteButton.Visibility = uiState is LauncherUiState.Ready or LauncherUiState.OfflineReady
            ? Visibility.Visible
            : Visibility.Collapsed;

        ActionButton.Content = uiState == LauncherUiState.UpdateAvailable ? "업데이트" : "설치하기";
        ActionButton.IsEnabled = !isBusy && ActionButton.Visibility == Visibility.Visible;
        LaunchButton.IsEnabled = !isBusy && LaunchButton.Visibility == Visibility.Visible;
        DeleteButton.IsEnabled = !isBusy && DeleteButton.Visibility == Visibility.Visible;
    }

    private void Log(string message)
    {
        LogTextBox.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ScrollLogToEnd();
    }

    private void ResetInstallProgressLog()
    {
        lastLoggedProgressPercent = -1;
        lastLoggedProgressMessage = "";
        lastLoggedProgressAt = DateTimeOffset.MinValue;
    }

    private void LogInstallProgress(InstallProgress progress)
    {
        var wholePercent = (int)Math.Floor(progress.Percent);
        var now = DateTimeOffset.Now;
        var messageChanged = !string.Equals(lastLoggedProgressMessage, progress.Message, StringComparison.Ordinal);
        var percentChanged = wholePercent != lastLoggedProgressPercent;
        var longEnoughSinceLastLog = now - lastLoggedProgressAt >= TimeSpan.FromSeconds(1);

        if (!messageChanged && !percentChanged && !longEnoughSinceLastLog && progress.Percent < 100)
        {
            return;
        }

        lastLoggedProgressPercent = wholePercent;
        lastLoggedProgressMessage = progress.Message;
        lastLoggedProgressAt = now;

        var detail = string.IsNullOrWhiteSpace(progress.Detail)
            ? ""
            : $" - {TrimMiddle(progress.Detail, 64)}";
        Log($"{progress.Message} {progress.Percent:0}%{detail}");
    }

    private void ScrollLogToEnd()
    {
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
        LogTextBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            LogTextBox.CaretIndex = LogTextBox.Text.Length;
            LogTextBox.ScrollToEnd();
        }), DispatcherPriority.Background);
    }

    private void UpdateInstallPathText(LauncherInstallState? state, bool hasInstalledExecutable)
    {
        if (hasInstalledExecutable && state != null)
        {
            InstallPathText.Text = $"설치 기준 위치{Environment.NewLine}{settings.InstallRoot}{Environment.NewLine}{Environment.NewLine}실행 파일 위치{Environment.NewLine}{state.InstallPath}";
            return;
        }

        InstallPathText.Text = $"설치 기준 위치{Environment.NewLine}{settings.InstallRoot}{Environment.NewLine}{Environment.NewLine}아직 설치된 앱이 없습니다.";
    }

    private static string FormatPackageSize(long bytes)
    {
        var gib = bytes / 1024d / 1024d / 1024d;
        var mib = bytes / 1024d / 1024d;
        return $"{gib:0.00} GB ({mib:0} MB)";
    }

    private static string TrimMiddle(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var prefixLength = Math.Max(1, maxLength / 2 - 2);
        var suffixLength = Math.Max(1, maxLength - prefixLength - 3);
        return $"{value[..prefixLength]}...{value[^suffixLength..]}";
    }

    private async Task RefreshBannersAsync()
    {
        try
        {
            if (!Uri.TryCreate(DefaultEventsUrl, UriKind.Absolute, out var eventsUri))
            {
                throw new InvalidOperationException("이벤트 배너 주소가 올바르지 않습니다.");
            }

            var manifest = await eventsClient.GetAsync(eventsUri, CancellationToken.None);
            var now = DateTimeOffset.Now;
            activeBanners = manifest.Banners.Where(banner => IsBannerDisplayable(banner, now)).ToList();
            currentBannerIndex = 0;
        }
        catch (Exception exception)
        {
            Log($"이벤트 배너 정보를 불러오지 못했습니다: {exception.Message}");
            activeBanners = new List<EventBanner>();
        }

        UpdateBannerUi();
    }

    private bool IsBannerDisplayable(EventBanner banner, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(banner.Id) || string.IsNullOrWhiteSpace(banner.Title))
        {
            return false;
        }

        if (banner.Type is not "event" and not "update")
        {
            Log($"배너 '{banner.Id}' type이 알 수 없어 표시하지 않습니다: {banner.Type}");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(banner.LinkUrl) &&
            !(Uri.TryCreate(banner.LinkUrl, UriKind.Absolute, out var linkUri) && linkUri.Scheme == Uri.UriSchemeHttps))
        {
            Log($"배너 '{banner.Id}' linkUrl이 https가 아니라 표시하지 않습니다.");
            return false;
        }

        if (banner.DisplayFrom is { } from && from > now)
        {
            return false;
        }

        if (banner.DisplayTo is { } to && to < now)
        {
            return false;
        }

        return true;
    }

    private void UpdateBannerUi()
    {
        bannerRotateTimer.Stop();
        bannerResumeTimer.Stop();

        if (activeBanners.Count == 0)
        {
            BannerRow.Visibility = Visibility.Collapsed;
            return;
        }

        BannerRow.Visibility = Visibility.Visible;
        if (currentBannerIndex >= activeBanners.Count)
        {
            currentBannerIndex = 0;
        }

        RenderBanner(activeBanners[currentBannerIndex]);

        var hasMultiple = activeBanners.Count > 1;
        BannerPrevButton.IsEnabled = hasMultiple;
        BannerNextButton.IsEnabled = hasMultiple;

        if (hasMultiple)
        {
            bannerRotateTimer.Start();
        }
    }

    private void RenderBanner(EventBanner banner)
    {
        var isUpdate = banner.Type == "update";
        var brush = BrushFromHex(isUpdate ? "#B08E75" : "#3F4A3A");

        BannerAccent.Background = brush;
        BannerChip.Background = brush;
        BannerChipText.Text = isUpdate ? "업데이트" : "이벤트";
        BannerTitle.Text = banner.Title;

        if (!string.IsNullOrWhiteSpace(banner.LinkUrl))
        {
            BannerClickArea.Cursor = Cursors.Hand;
            BannerClickArea.Tag = banner.LinkUrl;
        }
        else
        {
            BannerClickArea.Cursor = Cursors.Arrow;
            BannerClickArea.Tag = null;
        }
    }

    private void BannerRotateTimer_Tick(object? sender, EventArgs e)
    {
        ShowNextBanner();
    }

    private void BannerResumeTimer_Tick(object? sender, EventArgs e)
    {
        bannerResumeTimer.Stop();
        if (activeBanners.Count > 1)
        {
            bannerRotateTimer.Start();
        }
    }

    private void BannerPrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (activeBanners.Count == 0)
        {
            return;
        }

        currentBannerIndex = (currentBannerIndex - 1 + activeBanners.Count) % activeBanners.Count;
        RenderBanner(activeBanners[currentBannerIndex]);
        PauseBannerRotation();
    }

    private void BannerNextButton_Click(object sender, RoutedEventArgs e)
    {
        ShowNextBanner();
        PauseBannerRotation();
    }

    private void BannerClickArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // IsBannerDisplayable 가 fetch 시점에 https 검증하지만, 클릭 시점에 한 번 더
        // 확인 (defense in depth — RenderBanner 가 가드 우회해서 호출되는 회귀 방지).
        if (BannerClickArea.Tag is not string url ||
            string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Log($"링크를 열지 못했습니다: {exception.Message}");
        }
    }

    private void ShowNextBanner()
    {
        if (activeBanners.Count == 0)
        {
            return;
        }

        currentBannerIndex = (currentBannerIndex + 1) % activeBanners.Count;
        RenderBanner(activeBanners[currentBannerIndex]);
    }

    private void PauseBannerRotation()
    {
        bannerRotateTimer.Stop();
        bannerResumeTimer.Stop();
        if (activeBanners.Count > 1)
        {
            bannerResumeTimer.Start();
        }
    }

    private static Brush BrushFromHex(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
