const state = {
  settings: { hasOpenAiKey: false, battleGenerationMode: "dynamic" },
  runtime: { allowMockWithoutKey: true, forceMockMode: false, displayName: "ことばコロシアム" },
  battle: null,
  story: null,
  overlay: "title",
  encounterCount: 0,
  listening: false,
  recognition: null,
  generationProgressTimer: null,
};

const elements = {};

document.addEventListener("DOMContentLoaded", () => {
  captureElements();
  bindEvents();
  initialize().catch((error) => {
    renderSettingsResult(`初期化に失敗しました: ${error.message}`, "error");
  });
});

async function initialize() {
  await Promise.all([loadSettings(), loadRuntime()]);
  setupSpeech();
  renderSettingsStatus();
  renderTitleScreen();
  renderBattleFrame();
  setOverlay("title");
}

function captureElements() {
  const ids = [
    "settings-status-badge",
    "settings-panel-badge",
    "runtime-mode-badge",
    "run-counter",
    "home-status",
    "settings-result",
    "openai-key-input",
    "toggle-key-visibility-button",
    "battle-generation-mode",
    "save-mode-button",
    "save-key-button",
    "test-key-button",
    "delete-key-button",
    "start-battle-button",
    "jump-settings-button",
    "open-settings-hero-button",
    "story-settings-button",
    "settings-close-button",
    "settings-backdrop",
    "battle-panel",
    "battle-bg-image",
    "enemy-name",
    "hp-text",
    "hp-fill",
    "enemy-line",
    "battle-stage",
    "enemy-portrait",
    "enemy-portrait-shell",
    "provider-badge",
    "enemy-persona-summary",
    "battle-preview-title",
    "battle-preview-description",
    "battle-mode-summary",
    "battle-mode-description",
    "attack-input",
    "attack-button",
    "input-dock",
    "speech-button",
    "speech-status",
    "generation-progress",
    "generation-progress-label",
    "generation-progress-value",
    "generation-progress-fill",
    "title-screen",
    "loading-screen",
    "story-screen",
    "story-phase-label",
    "story-title",
    "story-text",
    "story-continue-button",
  ];

  for (const id of ids) {
    elements[id] = document.getElementById(id);
  }
}

function bindEvents() {
  for (const id of ["jump-settings-button", "open-settings-hero-button", "story-settings-button"]) {
    elements[id].addEventListener("click", openSettings);
  }

  elements["settings-close-button"].addEventListener("click", closeSettings);
  elements["settings-backdrop"].addEventListener("click", (event) => {
    if (event.target === elements["settings-backdrop"]) {
      closeSettings();
    }
  });

  elements["toggle-key-visibility-button"].addEventListener("click", () => {
    const input = elements["openai-key-input"];
    input.type = input.type === "password" ? "text" : "password";
    elements["toggle-key-visibility-button"].textContent = input.type === "password" ? "表示" : "非表示";
  });

  elements["save-mode-button"].addEventListener("click", saveBattleGenerationMode);
  elements["save-key-button"].addEventListener("click", saveKey);
  elements["test-key-button"].addEventListener("click", testKey);
  elements["delete-key-button"].addEventListener("click", deleteKey);
  elements["start-battle-button"].addEventListener("click", () => requestBattleStart(elements["start-battle-button"]));
  elements["story-continue-button"].addEventListener("click", handleStoryContinue);
  elements["attack-button"].addEventListener("click", attack);
  elements["speech-button"].addEventListener("click", toggleSpeech);
  elements["attack-input"].addEventListener("keydown", (event) => {
    if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      attack();
    }
  });
}

async function loadSettings() {
  const response = await fetch("/api/settings");
  if (!response.ok) {
    throw new Error("設定状態の取得に失敗しました。");
  }

  state.settings = await response.json();
}

async function loadRuntime() {
  const response = await fetch("/api/runtime");
  if (!response.ok) {
    return;
  }

  state.runtime = await response.json();
}

function renderSettingsStatus() {
  const configured = state.settings.hasOpenAiKey;
  const label = configured ? "設定済み" : "未設定";
  const badgeClass = configured ? "success" : "warning";
  elements["battle-generation-mode"].value = state.settings.battleGenerationMode || "dynamic";

  for (const id of ["settings-status-badge", "settings-panel-badge"]) {
    elements[id].textContent = label;
    elements[id].className = `status-badge ${badgeClass}`;
  }

  const modeText = state.runtime.forceMockMode
    ? "force mock"
    : configured
      ? "openai"
      : "mock";

  elements["runtime-mode-badge"].textContent = modeText;
  elements["runtime-mode-badge"].className = `status-badge ${configured ? "success" : "neutral"}`;
}

function renderTitleScreen() {
  const configured = state.settings.hasOpenAiKey;
  const mode = state.settings.battleGenerationMode || "dynamic";

  elements["battle-mode-summary"].textContent = mode;

  if (mode === "fixed") {
    elements["battle-preview-title"].textContent = "固定敵チャレンジ";
    elements["battle-preview-description"].textContent = "スクロールなしの単画面で、固定敵をテンポよく倒していくモードです。";
    elements["battle-mode-description"].textContent = "固定敵で安定起動します。倒した後は次戦へすぐ進めます。";
  } else {
    elements["battle-preview-title"].textContent = "連戦コロシアム";
    elements["battle-preview-description"].textContent = "毎戦ごとに敵、前後ストーリー、背景、立ち絵をその場生成します。";
    elements["battle-mode-description"].textContent = "dynamic では敵が次々に現れ、失敗時だけ fixed へ自動フォールバックします。";
  }

  if (state.runtime.forceMockMode) {
    elements["home-status"].textContent = "現在は強制 mock mode です。導線確認や E2E 用の安全レーンとして動作します。";
    elements["home-status"].className = "callout warning";
    return;
  }

  if (configured) {
    elements["home-status"].textContent = mode === "dynamic"
      ? "OpenAI API key は保存済みです。ゲーム開始で物語が流れ、敵が現れ、倒すと次の敵へ進めます。"
      : "OpenAI API key は保存済みです。現在は fixed モードなので、固定敵で安定して連戦できます。";
    elements["home-status"].className = "callout success";
    return;
  }

  elements["home-status"].textContent = mode === "dynamic"
    ? "OpenAI API key は未設定です。dynamic は fixed に自動フォールバックします。設定を入れると毎戦ごとの動的生成を使えます。"
    : "OpenAI API key は未設定ですが、fixed モードなのでそのまま遊べます。";
  elements["home-status"].className = "callout warning";
}

function renderBattleFrame(options = {}) {
  const battle = state.battle;
  if (!battle) {
    setImmediateText(elements["enemy-name"], "敵未出現");
    setImmediateText(elements["enemy-persona-summary"], "ゲーム開始で敵が現れます。");
    setImmediateText(elements["enemy-line"], "敵が姿を見せるまで、舞台は静かだ。");
    elements["hp-text"].textContent = "-- / --";
    elements["hp-fill"].style.width = "0%";
    elements["provider-badge"].textContent = "standby";
    elements["provider-badge"].className = "status-badge neutral";
    elements["enemy-portrait"].removeAttribute("src");
    elements["battle-bg-image"].removeAttribute("src");
    elements["run-counter"].textContent = "開幕前";
    updateActionControls();
    return;
  }

  const { enemy } = battle;
  const latestEnemyLine = battle.history.filter((item) => item.speaker === "enemy").at(-1)?.text || battle.openingLine;
  elements["run-counter"].textContent = `第 ${battle.encounterNumber} 戦`;
  elements["enemy-name"].textContent = enemy.name;
  elements["enemy-persona-summary"].textContent = `${enemy.species} / ${enemy.archetype} / ${enemy.personaSummary} / 弱点: ${enemy.weakPoints.join("、")}`;

  if (options.animateEnemy) {
    typeText(elements["enemy-line"], latestEnemyLine, 22);
  } else {
    setImmediateText(elements["enemy-line"], latestEnemyLine);
  }

  elements["provider-badge"].textContent = battle.provider === "openai"
    ? "dynamic / openai"
    : battle.fellBackToFixed
      ? "dynamic -> fixed"
      : "fixed";
  elements["provider-badge"].className = `status-badge ${battle.provider === "openai" ? "success" : "neutral"}`;
  elements["enemy-portrait"].src = battle.enemyImage;
  elements["enemy-portrait"].alt = `${enemy.name} の画像`;
  elements["battle-bg-image"].src = battle.bgImage;
  elements["battle-bg-image"].alt = "戦闘背景";

  const hpRatio = Math.max(0, Math.min(1, enemy.currentHp / enemy.maxHp));
  elements["hp-text"].textContent = `${enemy.currentHp} / ${enemy.maxHp}`;
  elements["hp-fill"].style.width = `${hpRatio * 100}%`;
  updateActionControls();
}

function updateActionControls() {
  const canFight = state.overlay === "battle" && state.battle && !state.battle.victory;
  elements["input-dock"].classList.toggle("hidden", !canFight);
  elements["attack-input"].disabled = !canFight;
  elements["attack-button"].disabled = !canFight;
}

function setOverlay(mode) {
  state.overlay = mode;
  elements["title-screen"].classList.toggle("hidden", mode !== "title");
  elements["loading-screen"].classList.toggle("hidden", mode !== "loading");
  elements["story-screen"].classList.toggle("hidden", mode !== "story");
  updateActionControls();
}

function openSettings() {
  elements["settings-backdrop"].classList.remove("hidden");
}

function closeSettings() {
  elements["settings-backdrop"].classList.add("hidden");
}

async function saveKey() {
  const apiKey = elements["openai-key-input"].value.trim();
  if (!apiKey) {
    renderSettingsResult("API key を入力してください。", "error");
    return;
  }

  await runButtonAction(elements["save-key-button"], async () => {
    const response = await fetch("/api/settings/openai", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ apiKey }),
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || "保存に失敗しました。");
    }

    state.settings = payload;
    elements["openai-key-input"].value = "";
    renderSettingsStatus();
    renderTitleScreen();
    renderSettingsResult("OpenAI API key をローカル保存しました。", "success");
  });
}

async function saveBattleGenerationMode() {
  const mode = elements["battle-generation-mode"].value;

  await runButtonAction(elements["save-mode-button"], async () => {
    const response = await fetch("/api/settings/battle-mode", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ mode }),
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || "モード保存に失敗しました。");
    }

    state.settings = payload;
    renderSettingsStatus();
    renderTitleScreen();
    renderSettingsResult(`バトル生成モードを ${mode} に保存しました。`, "success");
  });
}

async function testKey() {
  await runButtonAction(elements["test-key-button"], async () => {
    const response = await fetch("/api/settings/openai/test", {
      method: "POST",
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || "疎通確認に失敗しました。");
    }

    renderSettingsResult(payload.message || "疎通確認を実行しました。", payload.success ? "success" : "error");
  });
}

async function deleteKey() {
  await runButtonAction(elements["delete-key-button"], async () => {
    const response = await fetch("/api/settings/openai", {
      method: "DELETE",
    });

    if (!response.ok && response.status !== 204) {
      throw new Error("削除に失敗しました。");
    }

    state.settings.hasOpenAiKey = false;
    renderSettingsStatus();
    renderTitleScreen();
    renderSettingsResult("保存済みの OpenAI API key を削除しました。", "success");
  });
}

async function requestBattleStart(button) {
  await runButtonAction(button, async () => {
    beginGenerationProgress();
    setOverlay("loading");

    try {
      const response = await fetch("/api/start-battle", { method: "POST" });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.error || "バトル開始に失敗しました。");
      }

      state.encounterCount += 1;
      state.battle = {
        encounterNumber: state.encounterCount,
        encounter: payload.encounter,
        enemy: payload.enemy || payload.encounter?.enemies?.[0],
        provider: payload.provider,
        generationMode: payload.generationMode,
        fellBackToFixed: payload.fellBackToFixed,
        worldIntro: payload.worldIntro,
        openingLine: payload.openingLine,
        enemyImagePrompt: payload.enemyImagePrompt,
        bgImagePrompt: payload.bgImagePrompt,
        bgImage: payload.bgImage,
        enemyImage: payload.enemyImage,
        history: [
          createHistoryEntry("world", payload.worldIntro),
          createHistoryEntry("enemy", payload.openingLine),
        ],
        victory: false,
        lastAttackReason: null,
      };

      renderBattleFrame();
      completeGenerationProgress(true);
      presentPrologueStory();

      if (payload.fellBackToFixed) {
        renderSettingsResult("dynamic 生成に失敗したため fixed モードへ自動フォールバックしました。", "warning");
      }
    } catch (error) {
      completeGenerationProgress(false);
      setOverlay(state.battle ? "battle" : "title");
      throw error;
    }
  });
}

function presentPrologueStory() {
  const { enemy, worldIntro } = state.battle;
  state.story = {
    phaseLabel: `第 ${state.battle.encounterNumber} 戦`,
    title: `${enemy.species}「${enemy.name}」が現れた`,
    text: [
      `ことばが刃になる舞台、第 ${state.battle.encounterNumber} 戦が始まる。`,
      worldIntro,
      `${enemy.species}「${enemy.name}」が前へ出た。${enemy.personaSummary}`,
    ].join("\n\n"),
    buttonText: "バトル開始",
    action: "enter-battle",
  };

  renderStoryScreen();
  setOverlay("story");
}

function presentVictoryStory() {
  const { enemy, encounterNumber, lastAttackReason } = state.battle;
  state.story = {
    phaseLabel: `Victory ${encounterNumber}`,
    title: `${enemy.name} を打ち崩した`,
    text: [
      `${enemy.species}「${enemy.name}」はついに言葉を失い、舞台の熱が少し静まった。`,
      lastAttackReason || "見栄が砕け、戦場には勝利の余韻だけが残っている。",
      "次の敵の気配が近づいている。続けて進める。",
    ].join("\n\n"),
    buttonText: "次の敵へ",
    action: "next-battle",
  };

  renderStoryScreen();
  setOverlay("story");
}

function renderStoryScreen() {
  if (!state.story) {
    return;
  }

  elements["story-phase-label"].textContent = state.story.phaseLabel;
  elements["story-title"].textContent = state.story.title;
  elements["story-continue-button"].textContent = state.story.buttonText;
  typeText(elements["story-text"], state.story.text, 15);
}

async function handleStoryContinue() {
  if (!state.story) {
    return;
  }

  await runButtonAction(elements["story-continue-button"], async () => {
    if (state.story.action === "enter-battle") {
      state.story = null;
      setOverlay("battle");
      renderBattleFrame({ animateEnemy: true });
      elements["attack-input"].focus();
      return;
    }

    if (state.story.action === "next-battle") {
      state.story = null;
      await requestBattleStart(elements["story-continue-button"]);
    }
  });
}

async function attack() {
  if (!state.battle || state.battle.victory || state.overlay !== "battle") {
    return;
  }

  const playerText = elements["attack-input"].value.trim();
  if (!playerText) {
    renderSettingsResult("攻撃文を入力してください。", "error");
    return;
  }

  await runButtonAction(elements["attack-button"], async () => {
    const response = await fetch("/api/attack", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        enemy: state.battle.enemy,
        battleHistory: state.battle.history,
        playerText,
      }),
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || "攻撃に失敗しました。");
    }

    state.battle.provider = payload.provider || state.battle.provider;
    state.battle.history.push(createHistoryEntry("player", playerText));
    state.battle.history.push(createHistoryEntry("enemy", payload.enemyLine, payload.damage, payload.reason));
    state.battle.enemy.currentHp = Math.max(0, state.battle.enemy.currentHp - payload.damage);
    state.battle.victory = state.battle.enemy.currentHp <= 0;
    state.battle.lastAttackReason = payload.reason || null;

    elements["attack-input"].value = "";
    animateImpact(payload.animation);
    renderBattleFrame({ animateEnemy: true });

    if (state.battle.victory) {
      window.setTimeout(() => {
        presentVictoryStory();
      }, 640);
    }
  });
}

function setupSpeech() {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SpeechRecognition) {
    elements["speech-status"].textContent = "このブラウザでは Web Speech API が使えません。手入力で遊べます。";
    elements["speech-button"].disabled = true;
    return;
  }

  const recognition = new SpeechRecognition();
  recognition.lang = "ja-JP";
  recognition.interimResults = false;
  recognition.maxAlternatives = 1;

  recognition.onstart = () => {
    state.listening = true;
    elements["speech-status"].textContent = "音声を聞き取っています...";
    elements["speech-button"].textContent = "停止";
  };

  recognition.onend = () => {
    state.listening = false;
    elements["speech-status"].textContent = "音声入力待機中です。";
    elements["speech-button"].textContent = "音声入力";
  };

  recognition.onerror = (event) => {
    state.listening = false;
    elements["speech-status"].textContent = `音声入力に失敗しました: ${event.error}`;
    elements["speech-button"].textContent = "音声入力";
  };

  recognition.onresult = (event) => {
    const text = event.results?.[0]?.[0]?.transcript;
    if (text) {
      elements["attack-input"].value = text;
      elements["speech-status"].textContent = "音声入力を反映しました。";
    }
  };

  state.recognition = recognition;
  elements["speech-status"].textContent = "音声入力に対応しています。";
}

function toggleSpeech() {
  if (!state.recognition) {
    return;
  }

  if (state.listening) {
    state.recognition.stop();
    return;
  }

  state.recognition.start();
}

function renderSettingsResult(message, tone) {
  elements["settings-result"].textContent = message;
  elements["settings-result"].className = `callout ${tone}`;
}

function beginGenerationProgress() {
  clearGenerationProgressTimer();
  updateGenerationProgress(6);
  elements["generation-progress-label"].textContent = "生成中...";
  state.generationProgressTimer = window.setInterval(() => {
    const current = parseInt(elements["generation-progress-value"].textContent, 10) || 0;
    const next = current < 55
      ? current + 9
      : current < 80
        ? current + 4
        : current < 93
          ? current + 1
          : current;
    updateGenerationProgress(Math.min(next, 93));
  }, 420);
}

function completeGenerationProgress(success) {
  clearGenerationProgressTimer();
  updateGenerationProgress(100);
  elements["generation-progress-label"].textContent = success ? "生成完了" : "生成失敗";
}

function clearGenerationProgressTimer() {
  if (state.generationProgressTimer) {
    window.clearInterval(state.generationProgressTimer);
    state.generationProgressTimer = null;
  }
}

function updateGenerationProgress(value) {
  elements["generation-progress-value"].textContent = `${value}%`;
  elements["generation-progress-fill"].style.width = `${value}%`;
}

function typeText(element, text, delayMs) {
  const token = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  element.dataset.typingToken = token;
  element.textContent = "";

  const chars = [...(text || "")];
  chars.forEach((character, index) => {
    window.setTimeout(() => {
      if (element.dataset.typingToken !== token) {
        return;
      }

      element.textContent += character;
    }, index * delayMs);
  });
}

function setImmediateText(element, text) {
  element.dataset.typingToken = `instant-${Date.now()}`;
  element.textContent = text || "";
}

function animateImpact(animation) {
  const shell = elements["enemy-portrait-shell"];
  shell.classList.remove("hit", "critical", "defeat");
  void shell.offsetWidth;
  if (animation) {
    shell.classList.add(animation);
  }
}

async function runButtonAction(button, action) {
  button.disabled = true;
  try {
    await action();
  } catch (error) {
    renderSettingsResult(error.message || "処理に失敗しました。", "error");
  } finally {
    button.disabled = false;
  }
}

function createHistoryEntry(speaker, text, damage = null, reason = null) {
  return { speaker, text, damage, reason };
}
