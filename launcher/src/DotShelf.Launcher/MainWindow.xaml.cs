using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private const string LauncherDownloadUrl = "https://dotshelf.digital/download";
    private static readonly TimeSpan BannerRotationInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BannerManualPauseDuration = TimeSpan.FromSeconds(10);

    private readonly ManifestClient manifestClient = new();
    private readonly EventsClient eventsClient = new();
    private readonly LauncherSettings settings;
    private readonly DispatcherTimer gameTimer = new();
    private readonly DispatcherTimer bannerRotateTimer = new();
    private readonly DispatcherTimer bannerResumeTimer = new();
    private Process? launchedProcess;
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
        bannerRotateTimer.Start();
        bannerResumeTimer.Interval = BannerManualPauseDuration;
        bannerResumeTimer.Tick += BannerResumeTimer_Tick;
        UpdateGameHud();
        ApplyUiState(LauncherUiState.Checking);
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private enum LauncherUiState
    {
        Checking,
        NotInstalled,
        UpdateAvailable,
        Ready,
        OfflineReady,
        Running,
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

    private sealed class LauncherVersionNotSupportedException(string currentVersion, string minVersion)
        : InvalidOperationException($"Launcher {currentVersion} is lower than required version {minVersion}.")
    {
        public string CurrentVersion { get; } = currentVersion;
        public string MinVersion { get; } = minVersion;
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

    private void MiniGameCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenGameModal();
    }

    private void CloseGameButton_Click(object sender, RoutedEventArgs e)
    {
        CloseGameModal();
    }

    private void GameModalBackdrop_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CloseGameModal();
    }

    private void OpenGameModal()
    {
        // 모달 열기: 게임 시작 전 안내 화면 상태로 초기화.
        GameOverlay.Visibility = Visibility.Visible;
        GameOverlayTitle.Text = "하늘책 수집";
        GameOverlayDescription.Text = "책을 받으면 점수가 오르고, 어두운 장애물에 닿으면 게임이 끝납니다.";
        GameOverlayInstruction.Text = "← / → 방향키로 이동";
        StartGameButton.Visibility = Visibility.Visible;
        RetryGameButton.Visibility = Visibility.Collapsed;

        UpdateGameHud();
        GameModal.Visibility = Visibility.Visible;
        GameCanvas.Focus();
    }

    private void CloseGameModal()
    {
        // 게임이 돌고 있다면 정리하고 닫음.
        gameTimer.Stop();
        isGameRunning = false;
        isMovingLeft = false;
        isMovingRight = false;
        fallingGameItems.Clear();
        GameCanvas.Children.Clear();
        gamePlayer = null;

        GameModal.Visibility = Visibility.Collapsed;
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
            EnsureLauncherVersionSupported(manifest);
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
            StatusText.Text = "DotShelf 앱을 실행 중입니다.";
            Log($"실행된 프로세스: {process.Id}");
            TrackRunningProcess(process);
        });
    }

    private void TrackRunningProcess(Process process)
    {
        try
        {
            DetachLaunchedProcess();
            launchedProcess = process;
            process.EnableRaisingEvents = true;
            process.Exited += LaunchedProcess_Exited;
            ApplyUiState(LauncherUiState.Running);
        }
        catch (Exception ex)
        {
            Log($"실행 상태 추적에 실패했습니다: {ex.Message}");
        }
    }

    private void DetachLaunchedProcess()
    {
        if (launchedProcess is { } previous)
        {
            previous.Exited -= LaunchedProcess_Exited;
            previous.Dispose();
            launchedProcess = null;
        }
    }

    private void LaunchedProcess_Exited(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            DetachLaunchedProcess();
            Log("DotShelf 앱이 종료되었습니다.");
            await RefreshLauncherStateAsync(resetProgress: false);
        }));
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Window 종료 시 외부 프로세스 핸들 정리 — Dispatcher 도 곧 shutdown 되므로
        // Exited 콜백이 race 로 도착하기 전에 unsubscribe 해 둔다.
        DetachLaunchedProcess();
    }

    private void CycleBanner(int delta)
    {
        if (activeBanners.Count == 0) return;
        var total = activeBanners.Count;
        currentBannerIndex = ((currentBannerIndex + delta) % total + total) % total;
        ApplyBannerIndex();
        bannerRotateTimer.Stop();
        bannerRotateTimer.Start();
    }

    private void ApplyBannerIndex()
    {
        if (activeBanners.Count == 0)
        {
            HeroSlot.Visibility = Visibility.Collapsed;
            BannerRow.Visibility = Visibility.Collapsed;
            HeroDotsPanel.Children.Clear();
            return;
        }

        if (currentBannerIndex >= activeBanners.Count) currentBannerIndex = 0;

        HeroSlot.Visibility = Visibility.Visible;
        BannerRow.Visibility = Visibility.Visible;
        RenderBanner(activeBanners[currentBannerIndex]);

        RebuildHeroDots(activeBanners.Count, currentBannerIndex);
    }

    private void RebuildHeroDots(int total, int activeIdx)
    {
        HeroDotsPanel.Children.Clear();
        if (total <= 1) return;

        var on = (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")!);
        var off = (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#59FFFFFF")!);

        for (var i = 0; i < total; i++)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = i == activeIdx ? on : off,
                Margin = new Thickness(0, 0, i == total - 1 ? 0 : 8, 0)
            };
            HeroDotsPanel.Children.Add(dot);
        }
    }

    private void KeepCurrentLink_Click(object sender, MouseButtonEventArgs e)
    {
        // "이번 버전 그대로 실행" — UpdateAvailable 일 때 업데이트 건너뛰고 기존 버전 실행
        if (currentUiState != LauncherUiState.UpdateAvailable)
        {
            return;
        }
        LaunchButton_Click(sender, new RoutedEventArgs());
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
            EnsureLauncherVersionSupported(manifest);
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

            // mandatory:true 면 사용자 클릭 없이 자동으로 업데이트 진행.
            // BeginInvoke 로 다음 펌프 차례에 호출 — 현재 메서드의 finally(SetBusy(false)) 가
            // 먼저 정리된 뒤 ActionButton_Click 의 RunActionAsync 가 깨끗한 SetBusy(true) 사이클로 시작.
            if (manifest.Mandatory)
            {
                Log("필수 업데이트가 감지되어 자동으로 진행합니다.");
                // fire-and-forget: DispatcherOperation 결과는 무시 — async void 핸들러를
                // 다음 펌프 차례에 발동시키는 게 목적이고 현재 메서드는 finally 로 빠진다.
                _ = Dispatcher.BeginInvoke(new Action(() =>
                    ActionButton_Click(this, new RoutedEventArgs())));
            }
        }
        catch (LauncherVersionNotSupportedException exception)
        {
            Log($"Launcher update required: current {exception.CurrentVersion}, minimum {exception.MinVersion}");
            VersionText.Text = $"Launcher {exception.CurrentVersion}";
            PackageSizeText.Text = "설치 파일 크기: 확인 불가";
            StatusText.Text = $"런처를 먼저 업데이트해야 합니다. 최신 런처: {LauncherDownloadUrl}";
            ApplyUiState(LauncherUiState.Error);
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

    private static void EnsureLauncherVersionSupported(LauncherManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.MinLauncherVersion))
        {
            return;
        }

        var currentVersion = GetCurrentLauncherVersion();
        if (ReleaseVersionComparer.Compare(currentVersion, manifest.MinLauncherVersion) >= 0)
        {
            return;
        }

        throw new LauncherVersionNotSupportedException(currentVersion, manifest.MinLauncherVersion);
    }

    private static string GetCurrentLauncherVersion()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
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
        catch (LauncherVersionNotSupportedException exception)
        {
            StatusText.Text = $"런처 업데이트가 필요합니다. 최신 런처: {LauncherDownloadUrl}";
            Log($"Current launcher {exception.CurrentVersion} is lower than required {exception.MinVersion}.");
            ApplyUiState(LauncherUiState.Error);
            return false;
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

        // mandatory:true 면 사용자가 업데이트를 우회할 수 없도록 "이번 버전 그대로 실행" 링크를 숨긴다.
        // (자동 트리거는 RefreshLauncherStateAsync 에서 별도로 발동)
        var mandatoryUpdate = uiState == LauncherUiState.UpdateAvailable && latestManifest?.Mandatory == true;
        var showAction = uiState is LauncherUiState.NotInstalled or LauncherUiState.UpdateAvailable or LauncherUiState.Error;
        var showLaunch = uiState is LauncherUiState.Ready or LauncherUiState.OfflineReady;
        var showDelete = uiState is LauncherUiState.Ready or LauncherUiState.OfflineReady or LauncherUiState.UpdateAvailable;
        var showRunning = uiState == LauncherUiState.Running;
        var showError = uiState == LauncherUiState.Error;
        var showKeepCurrent = uiState == LauncherUiState.UpdateAvailable && !mandatoryUpdate;

        ActionButton.Visibility = showAction ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.Visibility = showLaunch ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = showDelete ? Visibility.Visible : Visibility.Collapsed;
        RunningIndicator.Visibility = showRunning ? Visibility.Visible : Visibility.Collapsed;
        ErrorAlert.Visibility = showError ? Visibility.Visible : Visibility.Collapsed;
        if (showError)
        {
            ErrorAlertText.Text = string.IsNullOrWhiteSpace(StatusText.Text)
                ? "오류가 발생했습니다. 다시 시도해 주세요."
                : StatusText.Text;
        }
        KeepCurrentLink.Visibility = showKeepCurrent ? Visibility.Visible : Visibility.Collapsed;
        MoreMenuToggle.Visibility = showRunning ? Visibility.Collapsed : Visibility.Visible;

        ActionButton.Content = uiState switch
        {
            LauncherUiState.UpdateAvailable => "업데이트",
            LauncherUiState.Error => "다시 시도",
            _ => "설치하기"
        };
        LaunchButton.Content = "▶  시작하기";

        CtaCaption.Text = uiState switch
        {
            LauncherUiState.NotInstalled => "디스크 1.2 GB 필요",
            LauncherUiState.UpdateAvailable => mandatoryUpdate ? "필수 업데이트" : "",
            LauncherUiState.Ready or LauncherUiState.OfflineReady =>
                latestManifest != null ? $"v{latestManifest.LatestVersion}" : "",
            LauncherUiState.Running => "창을 닫아도 계속 실행됩니다",
            LauncherUiState.Error => "지원이 필요하면 dotshelf.digital로 문의 주세요",
            _ => ""
        };

        ActionButton.IsEnabled = !isBusy && showAction;
        LaunchButton.IsEnabled = !isBusy && showLaunch;
        DeleteButton.IsEnabled = !isBusy && showDelete;
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

        if (banner.Type is not "event" and not "update" and not "notice" and not "collab" and not "collaboration")
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
        bannerResumeTimer.Stop();
        currentBannerIndex = 0;
        ApplyBannerIndex();

        var hasMultiple = activeBanners.Count > 1;
        BannerPrevButton.IsEnabled = hasMultiple;
        BannerNextButton.IsEnabled = hasMultiple;
    }

    private void RenderBanner(EventBanner banner)
    {
        // banner.Type 4종 매핑 — 디자인 §6 기반.
        var (label, chipHex, gradientStartHex, gradientEndHex, chipFgHex) = banner.Type?.ToLowerInvariant() switch
        {
            "update" => ("업데이트", "#5C9F6F", "#5C9F6F", "#3F7E54", "#FFFFFF"),
            "notice" => ("공지", "#6B5C50", "#2A2018", "#5A4538", "#FFFFFF"),
            "collab" or "collaboration"
                     => ("콜라보", "#C99450", "#D2BBA0", "#B79676", "#2A2018"),
            _        => ("이벤트", "#9F7E69", "#9F7E69", "#876653", "#FFFFFF")
        };

        BannerAccent.Background = BrushFromHex(chipHex);
        BannerChip.Background = BrushFromHex(chipHex);
        BannerChipText.Foreground = BrushFromHex(chipFgHex);
        BannerChipText.Text = label;
        BannerTitle.Text = banner.Title;
        BannerRow.Background = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(gradientStartHex)!,
            (Color)ColorConverter.ConvertFromString(gradientEndHex)!,
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 1));

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
        if (activeBanners.Count <= 1) return;
        currentBannerIndex = (currentBannerIndex + 1) % activeBanners.Count;
        ApplyBannerIndex();
    }

    private void BannerResumeTimer_Tick(object? sender, EventArgs e)
    {
        bannerResumeTimer.Stop();
        bannerRotateTimer.Start();
    }

    private void BannerPrevButton_Click(object sender, RoutedEventArgs e)
    {
        CycleBanner(-1);
        PauseBannerRotation();
    }

    private void BannerNextButton_Click(object sender, RoutedEventArgs e)
    {
        CycleBanner(+1);
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
