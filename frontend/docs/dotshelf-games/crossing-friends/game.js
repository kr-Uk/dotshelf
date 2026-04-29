(() => {
  const canvas = document.querySelector("#gameCanvas");
  const ctx = canvas.getContext("2d");

  const scoreValue = document.querySelector("#scoreValue");
  const bestValue = document.querySelector("#bestValue");
  const carrotValue = document.querySelector("#carrotValue");
  const overlay = document.querySelector("#gameOverlay");
  const overlayTitle = document.querySelector("#overlayTitle");
  const overlayScore = document.querySelector("#overlayScore");
  const startButton = document.querySelector("#startButton");
  const restartButton = document.querySelector("#restartButton");
  const soundButton = document.querySelector("#soundButton");

  const CONFIG = {
    cols: 11,
    visibleAhead: 26,
    backBuffer: 2,
    pruneBehind: 8,
    moveDuration: 0.13,
    wrapPadding: 3,
    bestKey: "carrot-bunny-best",
  };

  const MOVES = {
    up: { dx: 0, dy: 1, face: "up" },
    down: { dx: 0, dy: -1, face: "down" },
    left: { dx: -1, dy: 0, face: "left" },
    right: { dx: 1, dy: 0, face: "right" },
  };

  const laneColors = {
    grass: ["#8fdd74", "#7fd46e"],
    field: ["#f3d36f", "#eec964"],
    road: ["#b99a86", "#a98d80"],
    river: ["#70d1f0", "#58bfe3"],
  };

  let view = {
    w: 0,
    h: 0,
    dpr: 1,
    tile: 64,
    originX: 0,
    gridW: 0,
    baseY: 0,
  };

  let state = "start";
  let seed = 1;
  let lanes = new Map();
  let highestGenerated = -6;
  let player;
  let queuedMove = null;
  let particles = [];
  let cameraRow = -1;
  let targetCameraRow = -1;
  let maxReachedRow = 0;
  let carrotCount = 0;
  let carrotScore = 0;
  let combo = 0;
  let comboTimer = 0;
  let score = 0;
  let best = readBest();
  let lastTime = performance.now();
  let runningTime = 0;
  let muted = false;
  let audioCtx = null;
  let pointerStart = null;

  function readBest() {
    const saved = Number(localStorage.getItem(CONFIG.bestKey) || 0);
    return Number.isFinite(saved) ? saved : 0;
  }

  function saveBest(value) {
    best = Math.max(best, value);
    localStorage.setItem(CONFIG.bestKey, String(best));
  }

  function mulberry32(value) {
    return () => {
      let t = (value += 0x6d2b79f5);
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  function rowRandom(row) {
    return mulberry32((seed ^ Math.imul(row + 37, 0x9e3779b1)) >>> 0);
  }

  function choose(rand, items) {
    return items[Math.floor(rand() * items.length)];
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function lerp(a, b, t) {
    return a + (b - a) * t;
  }

  function easeOutBack(t) {
    const c1 = 1.6;
    const c3 = c1 + 1;
    return 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
  }

  function easeInOut(t) {
    return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
  }

  function resizeCanvas() {
    const rect = canvas.getBoundingClientRect();
    view.dpr = Math.min(window.devicePixelRatio || 1, 2);
    view.w = Math.max(320, rect.width);
    view.h = Math.max(360, rect.height);
    canvas.width = Math.floor(view.w * view.dpr);
    canvas.height = Math.floor(view.h * view.dpr);
    ctx.setTransform(view.dpr, 0, 0, view.dpr, 0, 0);
    view.tile = Math.floor(Math.min((view.w - 24) / CONFIG.cols, (view.h - 28) / 11));
    view.tile = clamp(view.tile, 38, 72);
    view.gridW = view.tile * CONFIG.cols;
    view.originX = (view.w - view.gridW) / 2;
    view.baseY = view.h - view.tile * 0.8;
  }

  function resetGame(nextState = "start") {
    seed = (Date.now() ^ Math.floor(Math.random() * 0xffffffff)) >>> 0;
    lanes = new Map();
    highestGenerated = -6;
    particles = [];
    player = {
      x: Math.floor(CONFIG.cols / 2),
      row: 0,
      face: "up",
      moving: false,
      move: null,
      bump: 0,
      blink: 0,
      overTilt: 0,
    };
    queuedMove = null;
    cameraRow = -1;
    targetCameraRow = -1;
    maxReachedRow = 0;
    carrotCount = 0;
    carrotScore = 0;
    combo = 0;
    comboTimer = 0;
    score = 0;
    state = nextState;
    ensureLanes(CONFIG.visibleAhead);
    updateHud();
    updateOverlay();
  }

  function ensureLanes(targetRow) {
    while (highestGenerated < targetRow) {
      highestGenerated += 1;
      lanes.set(highestGenerated, createLane(highestGenerated));
    }
  }

  function laneAt(row) {
    ensureLanes(row);
    return lanes.get(row);
  }

  function createLane(row) {
    const rand = rowRandom(row);
    const difficulty = difficultyForRow(row);

    if (row <= 1) {
      return createSafeLane(row, rand, row === 1 ? "field" : "grass", difficulty, true);
    }

    const hazardRun = countHazardRun(row);
    if (row % 10 === 0 || hazardRun >= 4) {
      return createSafeLane(row, rand, rand() > 0.45 ? "field" : "grass", difficulty, false);
    }

    const previous = lanes.get(row - 1)?.type;
    let roll = rand();
    if (previous === "river") roll += 0.15;
    if (previous === "road") roll += 0.08;

    if (roll < 0.48 + difficulty * 0.22) {
      return createRoadLane(row, rand, difficulty);
    }

    if (roll < 0.7 + difficulty * 0.16) {
      return createRiverLane(row, rand, difficulty);
    }

    return createSafeLane(row, rand, rand() > 0.5 ? "field" : "grass", difficulty, false);
  }

  function difficultyForRow(row) {
    const progress = clamp((row - 3) / 90, 0, 1);
    return Math.pow(progress, 0.72);
  }

  function countHazardRun(row) {
    let count = 0;
    for (let next = row - 1; next >= row - 5; next -= 1) {
      const type = lanes.get(next)?.type;
      if (type === "road" || type === "river") count += 1;
      else break;
    }
    return count;
  }

  function createBaseLane(row, rand, type) {
    const colors = laneColors[type];
    return {
      row,
      type,
      color: colors[Math.floor(rand() * colors.length)],
      shade: rand(),
      direction: rand() > 0.5 ? 1 : -1,
      speed: 0,
      blocked: new Set(),
      vehicles: [],
      platforms: [],
      carrots: [],
      decor: createDecor(rand, type),
    };
  }

  function createSafeLane(row, rand, type, difficulty, empty) {
    const lane = createBaseLane(row, rand, type);
    const blockedTarget = empty ? 0 : Math.floor(rand() * (2 + difficulty * 3));
    const protectedCols = row <= 2 ? new Set([4, 5, 6]) : new Set();

    while (lane.blocked.size < blockedTarget) {
      const col = Math.floor(rand() * CONFIG.cols);
      if (!protectedCols.has(col)) lane.blocked.add(col);
    }

    maybeAddCarrot(lane, rand, type === "field" ? 0.62 : 0.42);
    return lane;
  }

  function createRoadLane(row, rand, difficulty) {
    const lane = createBaseLane(row, rand, "road");
    lane.direction = rand() > 0.5 ? 1 : -1;
    lane.speed = lane.direction * (1.35 + rand() * 1.05 + difficulty * 2.4);
    const count = Math.min(7, 3 + Math.floor(rand() * 2 + difficulty * 3.2));
    const spacing = (CONFIG.cols + CONFIG.wrapPadding * 2) / count;
    const colors = ["#ff9c52", "#77c4ec", "#f8788d", "#ffe05f", "#8edb7c"];
    const kinds = ["truck", "tractor", "cart", "van"];

    for (let i = 0; i < count; i += 1) {
      lane.vehicles.push({
        x: -CONFIG.wrapPadding + i * spacing + rand() * spacing * 0.52,
        length: rand() > 0.63 ? 1.65 + difficulty * 0.22 : 1.05 + rand() * 0.5,
        color: choose(rand, colors),
        kind: choose(rand, kinds),
        wobble: rand() * Math.PI * 2,
      });
    }

    maybeAddCarrot(lane, rand, 0.24);
    return lane;
  }

  function createRiverLane(row, rand, difficulty) {
    const lane = createBaseLane(row, rand, "river");
    lane.direction = rand() > 0.5 ? 1 : -1;
    lane.speed = lane.direction * (0.95 + rand() * 0.86 + difficulty * 1.55);
    const count = Math.max(2, Math.min(4, 3 + Math.floor(rand() * 2 - difficulty * 1.15)));
    const spacing = (CONFIG.cols + CONFIG.wrapPadding * 2) / count;

    for (let i = 0; i < count; i += 1) {
      lane.platforms.push({
        x: -CONFIG.wrapPadding + i * spacing + rand() * spacing * 0.55,
        length: clamp(1.24 + rand() * 1.18 - difficulty * 0.32, 0.95, 2.45),
        kind: rand() > 0.48 ? "lily" : "log",
        wobble: rand() * Math.PI * 2,
      });
    }

    return lane;
  }

  function createDecor(rand, type) {
    const count = type === "road" ? 6 : type === "river" ? 7 : 10;
    const decor = [];
    for (let i = 0; i < count; i += 1) {
      decor.push({
        x: rand() * CONFIG.cols,
        y: rand(),
        size: 0.5 + rand() * 0.8,
        kind: choose(rand, type === "river" ? ["wave", "spark"] : ["flower", "clover", "pebble"]),
        color: choose(rand, ["#ffffff", "#ffd0dc", "#ffef8a", "#6ac67c", "#9fdcf3"]),
      });
    }
    return decor;
  }

  function maybeAddCarrot(lane, rand, chance) {
    if (lane.row <= 2 || rand() > chance) return;
    const candidates = [];
    for (let col = 0; col < CONFIG.cols; col += 1) {
      if (!lane.blocked.has(col)) candidates.push(col);
    }
    if (!candidates.length) return;
    lane.carrots.push({
      col: choose(rand, candidates),
      collected: false,
      bob: rand() * Math.PI * 2,
    });
  }

  function pruneLanes() {
    const minRow = maxReachedRow - CONFIG.pruneBehind;
    for (const row of lanes.keys()) {
      if (row < minRow) lanes.delete(row);
    }
  }

  function startGame() {
    initAudio();
    resetGame("playing");
    overlay.classList.add("is-hidden");
    playTone(520, 0.05, "sine", 0.05);
  }

  function endGame(reason) {
    if (state !== "playing") return;
    state = "over";
    player.overTilt = reason === "river" ? -0.4 : 0.45;
    addBurst(player.x, player.row, reason === "river" ? "#b9f2ff" : "#ffd76e", 18);
    playGameOver();
    saveBest(score);
    updateHud();
    updateOverlay();
  }

  function updateOverlay() {
    if (state === "playing") {
      overlay.classList.add("is-hidden");
      return;
    }

    overlay.classList.remove("is-hidden");
    if (state === "over") {
      overlayTitle.textContent = "기록 " + score;
      overlayScore.textContent = "최고 " + best + " · 당근 " + carrotCount;
      startButton.textContent = "↻";
      startButton.setAttribute("aria-label", "다시 시작");
      return;
    }

    overlayTitle.textContent = "길건너친구들";
    overlayScore.textContent = "";
    startButton.textContent = "▶";
    startButton.setAttribute("aria-label", "시작");
  }

  function updateHud() {
    score = Math.max(0, maxReachedRow) + carrotScore;
    scoreValue.textContent = String(score);
    bestValue.textContent = String(Math.max(best, score));
    carrotValue.textContent = String(carrotCount);
  }

  function requestMove(name) {
    if (!MOVES[name]) return;

    if (state !== "playing") {
      startGame();
      queuedMove = name;
      return;
    }

    if (player.moving) {
      queuedMove = name;
      return;
    }

    tryStartMove(name);
  }

  function tryStartMove(name) {
    const move = MOVES[name];
    const currentCol = Math.round(player.x);
    const targetX = currentCol + move.dx;
    const targetRow = player.row + move.dy;

    if (
      targetX < 0 ||
      targetX >= CONFIG.cols ||
      targetRow < maxReachedRow - CONFIG.backBuffer ||
      isBlocked(targetRow, targetX)
    ) {
      bumpPlayer();
      return;
    }

    ensureLanes(targetRow + CONFIG.visibleAhead);
    player.face = move.face;
    player.moving = true;
    player.move = {
      startX: player.x,
      startRow: player.row,
      targetX,
      targetRow,
      elapsed: 0,
      duration: CONFIG.moveDuration,
    };
    playHop();
  }

  function isBlocked(row, col) {
    if (col < 0 || col >= CONFIG.cols) return true;
    return laneAt(row).blocked.has(col);
  }

  function bumpPlayer() {
    player.bump = 0.14;
    playTone(180, 0.035, "square", 0.025);
  }

  function finishMove() {
    const move = player.move;
    player.x = move.targetX;
    player.row = move.targetRow;
    player.moving = false;
    player.move = null;
    addDust(player.x, player.row);

    if (player.row > maxReachedRow) {
      maxReachedRow = player.row;
      ensureLanes(maxReachedRow + CONFIG.visibleAhead);
      pruneLanes();
      if (maxReachedRow > 0 && maxReachedRow % 25 === 0) {
        addBurst(player.x, player.row, "#fff38f", 24);
        playTone(720, 0.08, "triangle", 0.04);
      }
    }

    collectCarrots();
    checkCurrentTile();
    updateHud();

    if (state === "playing" && queuedMove) {
      const next = queuedMove;
      queuedMove = null;
      tryStartMove(next);
    }
  }

  function updateGame(dt) {
    runningTime += dt;
    updateMovingLanes(dt);
    updateParticles(dt);

    if (state === "playing") {
      comboTimer = Math.max(0, comboTimer - dt);
      if (comboTimer === 0) combo = 0;
      if (player.bump > 0) player.bump = Math.max(0, player.bump - dt);

      if (player.moving) {
        player.move.elapsed += dt;
        const t = clamp(player.move.elapsed / player.move.duration, 0, 1);
        const eased = easeOutBack(t);
        player.x = lerp(player.move.startX, player.move.targetX, eased);
        if (t >= 1) finishMove();
      } else {
        carryOnRiver(dt);
        checkCurrentTile();
        collectCarrots();
      }

      updateHud();
    }

    targetCameraRow = Math.max(-1, maxReachedRow - 2.35);
    cameraRow = lerp(cameraRow, targetCameraRow, 1 - Math.pow(0.0005, dt));
  }

  function updateMovingLanes(dt) {
    const wrapWidth = CONFIG.cols + CONFIG.wrapPadding * 2;
    const minX = -CONFIG.wrapPadding;
    const maxX = CONFIG.cols + CONFIG.wrapPadding;

    for (const lane of lanes.values()) {
      if (lane.type !== "road" && lane.type !== "river") continue;
      const list = lane.type === "road" ? lane.vehicles : lane.platforms;
      const speed = laneRuntimeSpeed(lane);
      for (const item of list) {
        item.x += speed * dt;
        while (item.x > maxX) item.x -= wrapWidth;
        while (item.x < minX) item.x += wrapWidth;
      }
    }
  }

  function laneRuntimeSpeed(lane) {
    const difficulty = difficultyForRow(maxReachedRow);
    const boost = lane.type === "road" ? 0.55 : 0.38;
    return lane.speed * (1 + difficulty * boost);
  }

  function carryOnRiver(dt) {
    const lane = laneAt(player.row);
    if (lane.type !== "river") return;

    const platform = platformAt(lane, player.x);
    if (!platform) {
      endGame("river");
      return;
    }

    player.x += laneRuntimeSpeed(lane) * dt;
    if (player.x < -0.45 || player.x > CONFIG.cols - 0.55) {
      endGame("river");
    }
  }

  function checkCurrentTile() {
    if (state !== "playing" || player.moving) return;
    const lane = laneAt(player.row);

    if (lane.type === "road" && vehicleAt(lane, player.x)) {
      endGame("road");
      return;
    }

    if (lane.type === "river" && !platformAt(lane, player.x)) {
      endGame("river");
    }
  }

  function vehicleAt(lane, x) {
    for (const vehicle of lane.vehicles) {
      if (wrappedOverlap(vehicle, x, 0.36)) return vehicle;
    }
    return null;
  }

  function platformAt(lane, x) {
    for (const platform of lane.platforms) {
      if (wrappedOverlap(platform, x, 0.28)) return platform;
    }
    return null;
  }

  function wrappedOverlap(item, x, padding) {
    const wrapWidth = CONFIG.cols + CONFIG.wrapPadding * 2;
    for (const offset of [-wrapWidth, 0, wrapWidth]) {
      if (Math.abs(item.x + offset - x) <= item.length / 2 + padding) return true;
    }
    return false;
  }

  function collectCarrots() {
    const lane = laneAt(player.row);
    for (const carrot of lane.carrots) {
      if (carrot.collected) continue;
      if (Math.abs(player.x - carrot.col) < 0.42) {
        carrot.collected = true;
        carrotCount += 1;
        combo = comboTimer > 0 ? Math.min(combo + 1, 5) : 1;
        comboTimer = 4;
        carrotScore += 3 + combo * 2;
        addBurst(carrot.col, lane.row, "#ff9b3d", 16);
        playCollect();
      }
    }
  }

  function addDust(x, row) {
    for (let i = 0; i < 6; i += 1) {
      particles.push({
        x,
        row,
        ox: (Math.random() - 0.5) * view.tile * 0.22,
        oy: view.tile * 0.18,
        vx: (Math.random() - 0.5) * 30,
        vy: -20 - Math.random() * 25,
        life: 0.24,
        maxLife: 0.24,
        size: 2 + Math.random() * 3,
        color: "#f6f0c8",
      });
    }
  }

  function addBurst(x, row, color, count) {
    for (let i = 0; i < count; i += 1) {
      const angle = (Math.PI * 2 * i) / count + Math.random() * 0.4;
      const speed = 42 + Math.random() * 72;
      particles.push({
        x,
        row,
        ox: 0,
        oy: 0,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        life: 0.46 + Math.random() * 0.18,
        maxLife: 0.64,
        size: 2.5 + Math.random() * 3,
        color,
      });
    }
  }

  function updateParticles(dt) {
    for (const particle of particles) {
      particle.life -= dt;
      particle.ox += particle.vx * dt;
      particle.oy += particle.vy * dt;
      particle.vy += 90 * dt;
    }
    particles = particles.filter((particle) => particle.life > 0);
  }

  function draw() {
    ctx.clearRect(0, 0, view.w, view.h);
    drawBackground();
    drawLanes();
    drawParticles();
    drawPlayer();
    drawFrame();
  }

  function drawBackground() {
    const gradient = ctx.createLinearGradient(0, 0, 0, view.h);
    gradient.addColorStop(0, "#c9f1ff");
    gradient.addColorStop(0.56, "#effbdc");
    gradient.addColorStop(1, "#ffe9c4");
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, view.w, view.h);

    ctx.globalAlpha = 0.35;
    for (let i = 0; i < 8; i += 1) {
      const x = ((i * 137 + runningTime * 12) % (view.w + 160)) - 80;
      const y = 26 + (i % 3) * 34;
      drawCloud(x, y, 0.62 + (i % 2) * 0.2);
    }
    ctx.globalAlpha = 1;
  }

  function drawLanes() {
    const firstRow = Math.floor(cameraRow) - 4;
    const lastRow = Math.ceil(cameraRow + view.h / view.tile) + 6;
    ensureLanes(lastRow);

    for (let row = firstRow; row <= lastRow; row += 1) {
      const lane = laneAt(row);
      const y = screenY(row);
      if (y < -view.tile || y > view.h + view.tile) continue;
      drawLaneBase(lane, y);
      drawLaneDecor(lane, y);
      if (lane.type === "river") drawPlatforms(lane, y);
      drawCarrots(lane, y);
      if (lane.type === "road") drawVehicles(lane, y);
      drawStaticBlocks(lane, y);
      drawBackLimit(row, y);
    }
  }

  function drawLaneBase(lane, y) {
    const x = view.originX;
    const top = y - view.tile / 2;
    const h = view.tile;

    ctx.fillStyle = lane.color;
    ctx.fillRect(x, top, view.gridW, h + 1);

    if (lane.type === "field") {
      ctx.fillStyle = "rgba(142, 98, 31, 0.13)";
      for (let i = 0; i < CONFIG.cols; i += 1) {
        const cx = screenX(i);
        ctx.fillRect(cx - view.tile * 0.42, top + view.tile * 0.24, view.tile * 0.84, 3);
        ctx.fillRect(cx - view.tile * 0.42, top + view.tile * 0.62, view.tile * 0.84, 3);
      }
    }

    if (lane.type === "road") {
      ctx.fillStyle = "rgba(255, 255, 255, 0.45)";
      const dashW = view.tile * 0.48;
      for (let px = x + view.tile * 0.18; px < x + view.gridW; px += view.tile * 1.25) {
        roundRect(px, y - 3, dashW, 6, 3);
        ctx.fill();
      }
      ctx.fillStyle = "rgba(80, 62, 55, 0.12)";
      ctx.fillRect(x, top, view.gridW, 3);
      ctx.fillRect(x, top + h - 3, view.gridW, 3);
    }

    if (lane.type === "river") {
      ctx.fillStyle = "rgba(255, 255, 255, 0.27)";
      for (let i = 0; i < CONFIG.cols + 2; i += 1) {
        const waveX = x + (i - 0.3) * view.tile + Math.sin(runningTime * 2 + i) * 6;
        ctx.beginPath();
        ctx.arc(waveX, y - view.tile * 0.15, 8, 0, Math.PI, false);
        ctx.arc(waveX + 16, y - view.tile * 0.15, 8, Math.PI, 0, true);
        ctx.strokeStyle = "rgba(255, 255, 255, 0.34)";
        ctx.lineWidth = 2;
        ctx.stroke();
      }
    }

    ctx.strokeStyle = "rgba(255, 255, 255, 0.22)";
    ctx.lineWidth = 1;
    for (let col = 0; col <= CONFIG.cols; col += 1) {
      const gx = view.originX + col * view.tile;
      ctx.beginPath();
      ctx.moveTo(gx, top);
      ctx.lineTo(gx, top + h);
      ctx.stroke();
    }
  }

  function drawLaneDecor(lane, y) {
    if (lane.type === "road") {
      ctx.fillStyle = "rgba(78, 60, 54, 0.22)";
      for (const item of lane.decor) {
        drawPebble(screenX(item.x), y - view.tile * 0.35 + item.y * view.tile * 0.7, item.size);
      }
      return;
    }

    if (lane.type === "river") {
      for (const item of lane.decor) {
        const px = screenX(item.x);
        const py = y - view.tile * 0.36 + item.y * view.tile * 0.72;
        ctx.fillStyle = "rgba(255, 255, 255, 0.32)";
        ctx.beginPath();
        ctx.ellipse(px, py, item.size * 4, item.size * 1.2, 0, 0, Math.PI * 2);
        ctx.fill();
      }
      return;
    }

    for (const item of lane.decor) {
      const px = screenX(item.x);
      const py = y - view.tile * 0.35 + item.y * view.tile * 0.7;
      if (item.kind === "flower") drawFlower(px, py, item.color, item.size);
      if (item.kind === "clover") drawClover(px, py, item.size);
      if (item.kind === "pebble") drawPebble(px, py, item.size);
    }
  }

  function drawPlatforms(lane, y) {
    const wrapWidth = CONFIG.cols + CONFIG.wrapPadding * 2;
    ctx.save();
    clipLane(y);
    for (const platform of lane.platforms) {
      for (const offset of [-wrapWidth, 0, wrapWidth]) {
        const px = screenX(platform.x + offset);
        if (px < view.originX - view.tile * 2 || px > view.originX + view.gridW + view.tile * 2) {
          continue;
        }
        if (platform.kind === "log") drawLog(px, y, platform.length);
        else drawLily(px, y, platform.length, platform.wobble);
      }
    }
    ctx.restore();
  }

  function drawVehicles(lane, y) {
    const wrapWidth = CONFIG.cols + CONFIG.wrapPadding * 2;
    ctx.save();
    clipLane(y);
    for (const vehicle of lane.vehicles) {
      for (const offset of [-wrapWidth, 0, wrapWidth]) {
        const px = screenX(vehicle.x + offset);
        if (px < view.originX - view.tile * 2 || px > view.originX + view.gridW + view.tile * 2) {
          continue;
        }
        drawVehicle(px, y + Math.sin(runningTime * 9 + vehicle.wobble) * 1.3, vehicle, lane.direction);
      }
    }
    ctx.restore();
  }

  function clipLane(y) {
    ctx.beginPath();
    ctx.rect(view.originX, y - view.tile / 2, view.gridW, view.tile);
    ctx.clip();
  }

  function drawStaticBlocks(lane, y) {
    for (const col of lane.blocked) {
      const x = screenX(col);
      if (lane.type === "field") drawFence(x, y);
      else drawBush(x, y);
    }
  }

  function drawCarrots(lane, y) {
    for (const carrot of lane.carrots) {
      if (carrot.collected) continue;
      const bob = Math.sin(runningTime * 4 + carrot.bob) * 3;
      drawCarrot(screenX(carrot.col), y - view.tile * 0.02 + bob, view.tile * 0.58);
    }
  }

  function drawBackLimit(row, y) {
    const blockedBehind = maxReachedRow - CONFIG.backBuffer;
    if (row >= blockedBehind) return;

    ctx.fillStyle = "rgba(255, 249, 226, 0.42)";
    ctx.fillRect(view.originX, y - view.tile / 2, view.gridW, view.tile);

    if (row === blockedBehind - 1) {
      ctx.fillStyle = "rgba(255, 122, 155, 0.58)";
      for (let i = 0; i < CONFIG.cols; i += 1) {
        const x = screenX(i);
        roundRect(x - view.tile * 0.35, y - view.tile * 0.2, view.tile * 0.7, 7, 4);
        ctx.fill();
      }
    }
  }

  function drawParticles() {
    for (const particle of particles) {
      const alpha = clamp(particle.life / particle.maxLife, 0, 1);
      ctx.globalAlpha = alpha;
      ctx.fillStyle = particle.color;
      ctx.beginPath();
      ctx.arc(screenX(particle.x) + particle.ox, screenY(particle.row) + particle.oy, particle.size, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  function drawPlayer() {
    const display = getPlayerDisplay();
    const x = screenX(display.x);
    const y = screenY(display.row);
    const hop = display.hop;
    const bump = player.bump > 0 ? Math.sin(player.bump * 80) * 2.6 : 0;
    const px = x + bump;
    const py = y - hop;
    const danger = isNearDanger();

    ctx.save();
    ctx.globalAlpha = 0.26;
    ctx.fillStyle = "#31523f";
    ctx.beginPath();
    ctx.ellipse(x, y + view.tile * 0.28, view.tile * 0.27, view.tile * 0.1, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;

    ctx.translate(px, py);
    ctx.rotate(state === "over" ? player.overTilt : 0);
    const scale = view.tile / 64;
    ctx.scale(scale, scale);

    drawRabbitBody(danger);

    ctx.restore();
  }

  function getPlayerDisplay() {
    if (!player.moving || !player.move) {
      return {
        x: player.x,
        row: player.row,
        hop: Math.sin(runningTime * 3) * 1.2,
      };
    }

    const t = clamp(player.move.elapsed / player.move.duration, 0, 1);
    const eased = easeInOut(t);
    return {
      x: lerp(player.move.startX, player.move.targetX, eased),
      row: lerp(player.move.startRow, player.move.targetRow, eased),
      hop: Math.sin(t * Math.PI) * view.tile * 0.34,
    };
  }

  function isNearDanger() {
    const lane = laneAt(player.row);
    if (lane.type !== "road") return false;
    for (const vehicle of lane.vehicles) {
      if (wrappedOverlap(vehicle, player.x, 1.05)) return true;
    }
    return false;
  }

  function drawRabbitBody(danger) {
    ctx.lineWidth = 3;
    ctx.strokeStyle = "#efc7d0";

    ctx.fillStyle = "#fffaf8";
    ctx.beginPath();
    ctx.ellipse(-17, -29, 9, 24, -0.22, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(17, -29, 9, 24, 0.22, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = "#ffdfe8";
    ctx.beginPath();
    ctx.ellipse(-17, -29, 4.6, 16, -0.22, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(17, -29, 4.6, 16, 0.22, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.ellipse(0, 7, 24, 23, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = "#ffe9ef";
    ctx.beginPath();
    ctx.ellipse(0, 13, 13, 11, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.ellipse(0, -7, 22, 19, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    ctx.fillStyle = "#ffd6df";
    ctx.beginPath();
    ctx.ellipse(-17, 2, 5, 3, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(17, 2, 5, 3, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#28323a";
    const eyeH = danger ? 8 : 6;
    ctx.beginPath();
    ctx.ellipse(-8, -8, 3.2, eyeH, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.ellipse(8, -8, 3.2, eyeH, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#ff8ca5";
    ctx.beginPath();
    ctx.ellipse(0, -1, 3.2, 2.3, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = "#cf7e8c";
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(0, 1);
    ctx.quadraticCurveTo(-3, 5, -7, 4);
    ctx.moveTo(0, 1);
    ctx.quadraticCurveTo(3, 5, 7, 4);
    ctx.stroke();

    ctx.fillStyle = "#fff";
    ctx.beginPath();
    ctx.ellipse(-16, 25, 9, 6, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.ellipse(16, 25, 9, 6, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  }

  function drawFrame() {
    ctx.strokeStyle = "rgba(255, 255, 255, 0.45)";
    ctx.lineWidth = 4;
    ctx.strokeRect(view.originX, -2, view.gridW, view.h + 4);
  }

  function drawCloud(x, y, scale) {
    ctx.fillStyle = "#ffffff";
    ctx.beginPath();
    ctx.ellipse(x, y + 10 * scale, 24 * scale, 12 * scale, 0, 0, Math.PI * 2);
    ctx.ellipse(x + 19 * scale, y + 4 * scale, 18 * scale, 14 * scale, 0, 0, Math.PI * 2);
    ctx.ellipse(x + 39 * scale, y + 11 * scale, 26 * scale, 13 * scale, 0, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawVehicle(x, y, vehicle, direction) {
    const width = vehicle.length * view.tile * 0.84;
    const height = view.tile * 0.52;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(direction, 1);

    ctx.fillStyle = "rgba(50, 42, 39, 0.18)";
    ctx.beginPath();
    ctx.ellipse(0, height * 0.42, width * 0.48, 5, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = vehicle.color;
    roundRect(-width / 2, -height / 2, width, height, 8);
    ctx.fill();

    ctx.fillStyle = "rgba(255, 255, 255, 0.65)";
    roundRect(-width * 0.22, -height * 0.36, width * 0.32, height * 0.34, 5);
    ctx.fill();

    if (vehicle.kind === "tractor") {
      ctx.fillStyle = "#6ab06e";
      roundRect(width * 0.02, -height * 0.58, width * 0.24, height * 0.36, 5);
      ctx.fill();
    }

    ctx.fillStyle = "#3b4650";
    for (const wheelX of [-width * 0.3, width * 0.32]) {
      ctx.beginPath();
      ctx.arc(wheelX, height * 0.27, 7, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#f2f2e9";
      ctx.beginPath();
      ctx.arc(wheelX, height * 0.27, 3, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#3b4650";
    }

    ctx.restore();
  }

  function drawLog(x, y, length) {
    const width = length * view.tile * 0.88;
    const height = view.tile * 0.42;
    ctx.fillStyle = "rgba(45, 57, 64, 0.15)";
    ctx.beginPath();
    ctx.ellipse(x, y + height * 0.34, width * 0.42, 5, 0, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = "#bf8551";
    roundRect(x - width / 2, y - height / 2, width, height, 999);
    ctx.fill();
    ctx.strokeStyle = "#98663d";
    ctx.lineWidth = 2;
    ctx.stroke();

    ctx.strokeStyle = "rgba(255,255,255,0.28)";
    ctx.beginPath();
    ctx.moveTo(x - width * 0.32, y - height * 0.12);
    ctx.lineTo(x + width * 0.34, y - height * 0.12);
    ctx.moveTo(x - width * 0.28, y + height * 0.12);
    ctx.lineTo(x + width * 0.28, y + height * 0.12);
    ctx.stroke();
  }

  function drawLily(x, y, length, wobble) {
    const pads = Math.max(1, Math.round(length));
    for (let i = 0; i < pads; i += 1) {
      const offset = (i - (pads - 1) / 2) * view.tile * 0.56;
      const tilt = Math.sin(runningTime * 2 + wobble + i) * 0.13;
      ctx.save();
      ctx.translate(x + offset, y);
      ctx.rotate(tilt);
      ctx.fillStyle = "#52b969";
      ctx.beginPath();
      ctx.ellipse(0, 0, view.tile * 0.28, view.tile * 0.2, 0, 0.25, Math.PI * 2 - 0.35);
      ctx.lineTo(0, 0);
      ctx.fill();
      ctx.fillStyle = "#ffd5df";
      ctx.beginPath();
      ctx.arc(5, -4, 3, 0, Math.PI * 2);
      ctx.arc(0, -8, 3, 0, Math.PI * 2);
      ctx.arc(-5, -4, 3, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  function drawBush(x, y) {
    ctx.fillStyle = "rgba(42, 74, 52, 0.16)";
    ctx.beginPath();
    ctx.ellipse(x, y + view.tile * 0.22, view.tile * 0.28, view.tile * 0.08, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#4fb96b";
    for (let i = -1; i <= 1; i += 1) {
      ctx.beginPath();
      ctx.arc(x + i * view.tile * 0.13, y + view.tile * 0.02 - Math.abs(i) * 3, view.tile * 0.15, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = "#ffb8c8";
    ctx.beginPath();
    ctx.arc(x - 5, y - 5, 2.5, 0, Math.PI * 2);
    ctx.arc(x + 8, y + 2, 2.5, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawFence(x, y) {
    const w = view.tile * 0.56;
    const h = view.tile * 0.38;
    ctx.fillStyle = "rgba(42, 74, 52, 0.15)";
    ctx.beginPath();
    ctx.ellipse(x, y + h * 0.58, w * 0.6, 4, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#fff7de";
    for (const px of [-w * 0.28, 0, w * 0.28]) {
      roundRect(x + px - 4, y - h * 0.32, 8, h, 3);
      ctx.fill();
    }
    roundRect(x - w * 0.46, y - 3, w * 0.92, 7, 3);
    ctx.fill();
    ctx.strokeStyle = "#d8b873";
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }

  function drawCarrot(x, y, size) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(-0.13);
    ctx.fillStyle = "#42aa5a";
    ctx.beginPath();
    ctx.ellipse(-4, -size * 0.28, size * 0.13, size * 0.26, -0.55, 0, Math.PI * 2);
    ctx.ellipse(4, -size * 0.28, size * 0.13, size * 0.26, 0.55, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#ff8a2a";
    ctx.beginPath();
    ctx.moveTo(0, size * 0.33);
    ctx.quadraticCurveTo(-size * 0.28, -size * 0.12, 0, -size * 0.28);
    ctx.quadraticCurveTo(size * 0.28, -size * 0.12, 0, size * 0.33);
    ctx.fill();
    ctx.strokeStyle = "rgba(183, 82, 18, 0.42)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(-size * 0.08, -size * 0.02);
    ctx.lineTo(size * 0.12, -size * 0.07);
    ctx.moveTo(-size * 0.04, size * 0.1);
    ctx.lineTo(size * 0.1, size * 0.06);
    ctx.stroke();
    ctx.restore();
  }

  function drawFlower(x, y, color, size) {
    ctx.fillStyle = color;
    for (let i = 0; i < 5; i += 1) {
      const angle = (Math.PI * 2 * i) / 5;
      ctx.beginPath();
      ctx.arc(x + Math.cos(angle) * size * 3, y + Math.sin(angle) * size * 3, size * 2, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = "#ffd75e";
    ctx.beginPath();
    ctx.arc(x, y, size * 1.7, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawClover(x, y, size) {
    ctx.fillStyle = "#49ad64";
    for (const [dx, dy] of [
      [-1, 0],
      [1, 0],
      [0, -1],
    ]) {
      ctx.beginPath();
      ctx.ellipse(x + dx * size * 3, y + dy * size * 3, size * 3, size * 2.5, 0, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawPebble(x, y, size) {
    ctx.fillStyle = "rgba(255, 255, 255, 0.4)";
    ctx.beginPath();
    ctx.ellipse(x, y, size * 4, size * 2.2, 0, 0, Math.PI * 2);
    ctx.fill();
  }

  function roundRect(x, y, w, h, r) {
    const radius = Math.min(r, Math.abs(w) / 2, Math.abs(h) / 2);
    ctx.beginPath();
    ctx.moveTo(x + radius, y);
    ctx.arcTo(x + w, y, x + w, y + h, radius);
    ctx.arcTo(x + w, y + h, x, y + h, radius);
    ctx.arcTo(x, y + h, x, y, radius);
    ctx.arcTo(x, y, x + w, y, radius);
    ctx.closePath();
  }

  function screenX(col) {
    return view.originX + (col + 0.5) * view.tile;
  }

  function screenY(row) {
    return view.baseY - (row - cameraRow) * view.tile;
  }

  function initAudio() {
    if (!audioCtx) {
      audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (audioCtx.state === "suspended") audioCtx.resume();
  }

  function playTone(frequency, duration, type = "sine", gainValue = 0.04) {
    if (muted || !audioCtx) return;
    const now = audioCtx.currentTime;
    const oscillator = audioCtx.createOscillator();
    const gain = audioCtx.createGain();
    oscillator.type = type;
    oscillator.frequency.setValueAtTime(frequency, now);
    gain.gain.setValueAtTime(gainValue, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    oscillator.connect(gain);
    gain.connect(audioCtx.destination);
    oscillator.start(now);
    oscillator.stop(now + duration);
  }

  function playHop() {
    playTone(420, 0.035, "sine", 0.025);
    setTimeout(() => playTone(560, 0.035, "sine", 0.018), 38);
  }

  function playCollect() {
    playTone(720, 0.05, "triangle", 0.04);
    setTimeout(() => playTone(980, 0.06, "triangle", 0.035), 50);
  }

  function playGameOver() {
    playTone(230, 0.09, "sine", 0.045);
    setTimeout(() => playTone(150, 0.12, "sine", 0.04), 90);
  }

  function bindControls() {
    const keyMap = {
      ArrowUp: "up",
      w: "up",
      W: "up",
      ArrowDown: "down",
      s: "down",
      S: "down",
      ArrowLeft: "left",
      a: "left",
      A: "left",
      ArrowRight: "right",
      d: "right",
      D: "right",
    };

    window.addEventListener("keydown", (event) => {
      const move = keyMap[event.key];
      if (!move) return;
      event.preventDefault();
      requestMove(move);
    });

    document.querySelectorAll("[data-move]").forEach((button) => {
      button.addEventListener("pointerdown", (event) => {
        event.preventDefault();
        requestMove(button.dataset.move);
      });
    });

    startButton.addEventListener("click", startGame);
    restartButton.addEventListener("click", startGame);
    soundButton.addEventListener("click", () => {
      initAudio();
      muted = !muted;
      soundButton.textContent = muted ? "×" : "♪";
    });

    canvas.addEventListener("pointerdown", (event) => {
      pointerStart = { x: event.clientX, y: event.clientY };
    });

    canvas.addEventListener("pointerup", (event) => {
      if (!pointerStart) return;
      const dx = event.clientX - pointerStart.x;
      const dy = event.clientY - pointerStart.y;
      pointerStart = null;
      const distance = Math.hypot(dx, dy);
      if (distance < 20) {
        requestMove("up");
        return;
      }
      if (Math.abs(dx) > Math.abs(dy)) requestMove(dx > 0 ? "right" : "left");
      else requestMove(dy > 0 ? "down" : "up");
    });

    window.addEventListener("resize", resizeCanvas);
    document.addEventListener("visibilitychange", () => {
      lastTime = performance.now();
    });
  }

  function loop(now) {
    const dt = Math.min(0.05, (now - lastTime) / 1000 || 0);
    lastTime = now;
    updateGame(dt);
    draw();
    requestAnimationFrame(loop);
  }

  resizeCanvas();
  bindControls();
  resetGame("start");
  updateHud();
  requestAnimationFrame(loop);
})();
