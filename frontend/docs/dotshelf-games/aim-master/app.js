const TARGET_LIFETIME_MS = 2000;
const TARGET_SIZE = 44;
const GRID_STEP = 66;
const EDGE_PADDING = 42;

const arena = document.querySelector("#arena");
const setupPanel = document.querySelector("#setupPanel");
const resultPanel = document.querySelector("#resultPanel");
const setupForm = document.querySelector("#setupForm");
const targetCountInput = document.querySelector("#targetCount");
const capacityText = document.querySelector("#capacityText");
const hitCountText = document.querySelector("#hitCount");
const goalCountText = document.querySelector("#goalCount");
const missCountText = document.querySelector("#missCount");
const timeText = document.querySelector("#timeText");
const resultTitle = document.querySelector("#resultTitle");
const resultTime = document.querySelector("#resultTime");
const resultGoal = document.querySelector("#resultGoal");
const resultGenerated = document.querySelector("#resultGenerated");
const resultAccuracy = document.querySelector("#resultAccuracy");
const restartButton = document.querySelector("#restartButton");
const settingsButton = document.querySelector("#settingsButton");

const state = {
  running: false,
  goal: 30,
  hits: 0,
  misses: 0,
  generated: 0,
  startTime: 0,
  elapsedMs: 0,
  targetTimerId: 0,
  frameId: 0,
  positions: [],
  currentTarget: null,
};

function getCapacity() {
  const rect = arena.getBoundingClientRect();
  const width = Math.max(0, rect.width - EDGE_PADDING * 2);
  const height = Math.max(0, rect.height - EDGE_PADDING * 2);
  const columns = Math.max(1, Math.floor(width / GRID_STEP) + 1);
  const rows = Math.max(1, Math.floor(height / GRID_STEP) + 1);

  return Math.max(1, columns * rows);
}

function updateCapacity() {
  const capacity = getCapacity();
  const recommendedMax = Math.max(1, Math.min(120, capacity));
  targetCountInput.max = String(recommendedMax);

  if (Number(targetCountInput.value) > recommendedMax) {
    targetCountInput.value = String(recommendedMax);
  }

  capacityText.textContent = `현재 화면 기준 최대 ${recommendedMax}개까지 겹치지 않게 배치됩니다.`;
}

function buildPositionPool() {
  const rect = arena.getBoundingClientRect();
  const minX = EDGE_PADDING;
  const maxX = Math.max(minX, rect.width - EDGE_PADDING);
  const minY = EDGE_PADDING;
  const maxY = Math.max(minY, rect.height - EDGE_PADDING);
  const positions = [];

  for (let y = minY; y <= maxY; y += GRID_STEP) {
    for (let x = minX; x <= maxX; x += GRID_STEP) {
      positions.push({ x, y });
    }
  }

  return shuffle(positions);
}

function shuffle(items) {
  const next = [...items];

  for (let index = next.length - 1; index > 0; index -= 1) {
    const randomIndex = Math.floor(Math.random() * (index + 1));
    [next[index], next[randomIndex]] = [next[randomIndex], next[index]];
  }

  return next;
}

function clampGoal(value) {
  const max = Number(targetCountInput.max) || getCapacity();
  const parsed = Number.parseInt(value, 10);

  if (Number.isNaN(parsed)) {
    return Math.min(max, 30);
  }

  return Math.min(max, Math.max(1, parsed));
}

function resetState(goal) {
  clearTimeout(state.targetTimerId);
  cancelAnimationFrame(state.frameId);
  removeCurrentTarget();

  state.running = true;
  state.goal = goal;
  state.hits = 0;
  state.misses = 0;
  state.generated = 0;
  state.elapsedMs = 0;
  state.startTime = performance.now();
  state.positions = buildPositionPool();
}

function startGame(goal) {
  resetState(goal);
  setupPanel.classList.add("hidden");
  resultPanel.classList.add("hidden");
  goalCountText.textContent = String(goal);
  updateHud();
  updateTimer();
  spawnTarget();
}

function spawnTarget() {
  if (!state.running) {
    return;
  }

  if (state.hits >= state.goal) {
    finishGame("completed");
    return;
  }

  removeCurrentTarget();

  const position = state.positions.pop();

  if (!position) {
    finishGame("capacity");
    return;
  }

  const target = document.createElement("button");
  target.type = "button";
  target.className = "target";
  target.style.left = `${position.x}px`;
  target.style.top = `${position.y}px`;
  target.setAttribute("aria-label", `${state.hits + 1}번째 목표`);
  target.addEventListener("click", handleTargetHit);

  state.generated += 1;
  state.currentTarget = target;
  arena.append(target);
  updateHud();

  clearTimeout(state.targetTimerId);
  state.targetTimerId = window.setTimeout(handleTargetTimeout, TARGET_LIFETIME_MS);
}

function handleTargetHit(event) {
  event.stopPropagation();

  if (!state.running) {
    return;
  }

  const target = state.currentTarget;
  const x = Number.parseFloat(target.style.left);
  const y = Number.parseFloat(target.style.top);

  clearTimeout(state.targetTimerId);
  state.hits += 1;
  createHitBurst(x, y);
  removeCurrentTarget();
  updateHud();

  if (state.hits >= state.goal) {
    finishGame("completed");
    return;
  }

  spawnTarget();
}

function handleTargetTimeout() {
  if (!state.running) {
    return;
  }

  state.misses += 1;
  removeCurrentTarget();
  updateHud();
  spawnTarget();
}

function removeCurrentTarget() {
  if (state.currentTarget) {
    state.currentTarget.remove();
    state.currentTarget = null;
  }
}

function createHitBurst(x, y) {
  const burst = document.createElement("span");
  burst.className = "hit-burst";
  burst.style.left = `${x}px`;
  burst.style.top = `${y}px`;
  arena.append(burst);
  window.setTimeout(() => burst.remove(), 280);
}

function updateHud() {
  hitCountText.textContent = String(state.hits);
  goalCountText.textContent = String(state.goal);
  missCountText.textContent = String(state.misses);
}

function updateTimer() {
  if (!state.running) {
    return;
  }

  state.elapsedMs = performance.now() - state.startTime;
  timeText.textContent = formatSeconds(state.elapsedMs);
  state.frameId = requestAnimationFrame(updateTimer);
}

function finishGame(reason) {
  state.running = false;
  clearTimeout(state.targetTimerId);
  cancelAnimationFrame(state.frameId);
  state.elapsedMs = performance.now() - state.startTime;
  removeCurrentTarget();
  updateHud();

  const accuracy = state.generated > 0 ? Math.round((state.hits / state.generated) * 100) : 0;

  resultTitle.textContent = reason === "completed" ? "완료" : "위치 한도 도달";
  resultTime.textContent = formatSeconds(state.elapsedMs);
  resultGoal.textContent = `${state.hits}/${state.goal}`;
  resultGenerated.textContent = String(state.generated);
  resultAccuracy.textContent = `${accuracy}%`;
  resultPanel.classList.remove("hidden");
}

function formatSeconds(ms) {
  return `${(ms / 1000).toFixed(2)}s`;
}

setupForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const goal = clampGoal(targetCountInput.value);
  targetCountInput.value = String(goal);
  startGame(goal);
});

targetCountInput.addEventListener("input", () => {
  if (targetCountInput.value === "") {
    return;
  }

  const max = Number(targetCountInput.max) || getCapacity();
  const parsed = Number.parseInt(targetCountInput.value, 10);

  if (Number.isNaN(parsed)) {
    return;
  }

  if (parsed > max) {
    targetCountInput.value = String(max);
  }

  if (parsed < 1) {
    targetCountInput.value = "1";
  }
});

restartButton.addEventListener("click", () => {
  startGame(state.goal);
});

settingsButton.addEventListener("click", () => {
  resultPanel.classList.add("hidden");
  setupPanel.classList.remove("hidden");
  updateCapacity();
});

window.addEventListener("resize", () => {
  if (!state.running) {
    updateCapacity();
  }
});

window.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && !setupPanel.classList.contains("hidden")) {
    targetCountInput.focus();
  }
});

updateCapacity();
updateHud();
