const state = {
  settings: { hasOpenAiKey: false, battleGenerationMode: "dynamic" },
  runtime: { allowMockWithoutKey: true, forceMockMode: false, displayName: "ことばコロシアム" },
  battle: null,
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
  renderHomeStatus();
  renderBattleModePreview();
}

function captureElements() {
  const ids = [
    "settings-status-badge",
    "settings-panel-badge",
    "runtime-mode-badge",
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
    "battle-panel",
    "battle-bg-image",
    "enemy-name",
    "hp-text",
    "hp-fill",
    "enemy-line",
    "world-intro",
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
    "battle-log",
    "speech-button",
    "speech-status",
    "generation-progress",
    "generation-progress-label",
    "generation-progress-value",
    "generation-progress-fill",
  ];

  for (const id of ids) {
    elements[id] = document.getElementById(id);
  }
}

function bindEvents() {
  elements["jump-settings-button"].addEventListener("click", () => {
    document.getElementById("settings-panel").scrollIntoView({ behavior: "smooth", block: "start" });
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
  elements["start-battle-button"].addEventListener("click", startBattle);
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

function renderHomeStatus() {
  const configured = state.settings.hasOpenAiKey;
  const mode = state.settings.battleGenerationMode || "dynamic";

  if (state.runtime.forceMockMode) {
    elements["home-status"].textContent = "現在は強制 mock mode です。配信前導線や E2E ではこのモードを使えます。";
    elements["home-status"].className = "callout warning";
    return;
  }

  if (configured) {
    elements["home-status"].textContent = mode === "dynamic"
      ? "OpenAI API key は保存済みです。敵キャラ、導入、画像プロンプトをその場生成できます。"
      : "OpenAI API key は保存済みです。現在は fixed モードなので固定敵を安定起動します。";
    elements["home-status"].className = "callout success";
    return;
  }

  elements["home-status"].textContent = mode === "dynamic"
    ? "OpenAI API key は未設定です。dynamic は fixed に自動フォールバックします。先に設定すると動的生成を使えます。"
    : "OpenAI API key は未設定ですが、fixed モードなので固定敵でそのまま遊べます。";
  elements["home-status"].className = "callout warning";
}

function renderBattleModePreview() {
  const mode = state.settings.battleGenerationMode || "dynamic";
  elements["battle-mode-summary"].textContent = mode;

  if (mode === "fixed") {
    elements["battle-preview-title"].textContent = "固定敵バトル";
    elements["battle-preview-description"].textContent = "焼きそば食べたいマン戦を安定起動します。";
    elements["battle-mode-description"].textContent = "既存の固定敵・固定導入で戦います。OpenAI 失敗時の安全レーンでもあります。";
    return;
  }

  elements["battle-preview-title"].textContent = "動的生成バトル";
  elements["battle-preview-description"].textContent = "OpenAI がその場で敵キャラと導入を生成します。失敗時は固定敵モードへ自動フォールバックします。";
  elements["battle-mode-description"].textContent = "毎回その場でキャラクター、導入、画像プロンプトを生成します。";
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
    renderHomeStatus();
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
    renderHomeStatus();
    renderBattleModePreview();
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
    renderHomeStatus();
    renderSettingsResult("保存済みの OpenAI API key を削除しました。", "success");
  });
}

async function startBattle() {
  await runButtonAction(elements["start-battle-button"], async () => {
    beginGenerationProgress();
    try {
      const response = await fetch("/api/start-battle", { method: "POST" });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.error || "バトル開始に失敗しました。");
      }

      state.battle = {
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
        log: [
          createLogEntry("world", payload.worldIntro),
          createLogEntry("enemy", payload.openingLine),
        ],
        victory: false,
      };

      completeGenerationProgress(true);
      renderBattleState({ animateIntro: true, animateEnemy: true });
      elements["battle-panel"].classList.remove("hidden");
      elements["battle-panel"].scrollIntoView({ behavior: "smooth", block: "start" });

      if (payload.fellBackToFixed) {
        renderSettingsResult("dynamic 生成に失敗したため fixed モードへ自動フォールバックしました。", "warning");
      }
    } catch (error) {
      completeGenerationProgress(false);
      throw error;
    }
  });
}

async function attack() {
  if (!state.battle || state.battle.victory) {
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
        battleHistory: state.battle.log.map((entry) => ({
          speaker: entry.speaker,
          text: entry.text,
          damage: entry.damage,
          reason: entry.reason,
        })),
        playerText,
      }),
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.error || "攻撃に失敗しました。");
    }

    state.battle.provider = payload.provider || state.battle.provider;
    state.battle.log.push(createLogEntry("player", playerText));
    state.battle.log.push(createLogEntry("enemy", payload.enemyLine, payload.damage, payload.reason));
    state.battle.enemy.currentHp = Math.max(0, state.battle.enemy.currentHp - payload.damage);
    state.battle.victory = state.battle.enemy.currentHp <= 0;

    elements["attack-input"].value = "";
    animateImpact(payload.animation);
    renderBattleState({ animateEnemy: true });

    if (state.battle.victory) {
      renderSettingsResult("勝利。もう一度遊ぶ場合は「ゲーム開始」を押してください。", "success");
    }
  });

  if (state.battle?.victory) {
    elements["attack-input"].disabled = true;
    elements["attack-button"].disabled = true;
  }
}

function renderBattleState(options = {}) {
  if (!state.battle) {
    return;
  }

  const { enemy } = state.battle;
  elements["enemy-name"].textContent = enemy.name;
  elements["enemy-persona-summary"].textContent = `${enemy.species} / ${enemy.archetype} / ${enemy.personaSummary} / 弱点: ${enemy.weakPoints.join("、")}`;
  const latestEnemyLine = state.battle.log.filter((item) => item.speaker === "enemy").at(-1)?.text || state.battle.openingLine;
  if (options.animateIntro) {
    typeText(elements["world-intro"], state.battle.worldIntro, 16);
  } else {
    elements["world-intro"].textContent = state.battle.worldIntro;
  }

  if (options.animateEnemy) {
    typeText(elements["enemy-line"], latestEnemyLine, 22);
  } else {
    elements["enemy-line"].textContent = latestEnemyLine;
  }

  elements["provider-badge"].textContent = state.battle.provider === "openai"
    ? "dynamic / openai"
    : state.battle.fellBackToFixed
      ? "dynamic -> fixed"
      : "fixed";
  elements["provider-badge"].className = `status-badge ${state.battle.provider === "openai" ? "success" : "neutral"}`;
  elements["enemy-portrait"].src = state.battle.enemyImage;
  elements["enemy-portrait"].alt = `${enemy.name} の画像`;
  elements["battle-bg-image"].src = state.battle.bgImage;
  elements["battle-bg-image"].alt = "戦闘背景";

  const hpRatio = Math.max(0, Math.min(1, enemy.currentHp / enemy.maxHp));
  elements["hp-text"].textContent = `${enemy.currentHp} / ${enemy.maxHp}`;
  elements["hp-fill"].style.width = `${hpRatio * 100}%`;
  elements["attack-input"].disabled = state.battle.victory;
  elements["attack-button"].disabled = state.battle.victory;

  renderBattleLog();
}

function renderBattleLog() {
  elements["battle-log"].replaceChildren();
  const template = document.getElementById("log-entry-template");

  for (const entry of state.battle.log) {
    const fragment = template.content.cloneNode(true);
    fragment.querySelector(".log-speaker").textContent = speakerLabel(entry.speaker);
    fragment.querySelector(".log-text").textContent = entry.text;

    const meta = [];
    if (typeof entry.damage === "number") {
      meta.push(`damage ${entry.damage}`);
    }
    if (entry.reason) {
      meta.push(entry.reason);
    }
    fragment.querySelector(".log-meta").textContent = meta.join(" / ");

    elements["battle-log"].appendChild(fragment);
  }

  elements["battle-log"].scrollTop = elements["battle-log"].scrollHeight;
}

function animateImpact(animation) {
  const shell = elements["enemy-portrait-shell"];
  shell.classList.remove("hit", "critical", "defeat");
  void shell.offsetWidth;
  if (animation) {
    shell.classList.add(animation);
  }
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
  elements["generation-progress"].classList.remove("hidden");
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
  window.setTimeout(() => {
    elements["generation-progress"].classList.add("hidden");
    updateGenerationProgress(0);
    elements["generation-progress-label"].textContent = "生成中...";
  }, success ? 520 : 900);
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

async function runButtonAction(button, action) {
  const buttons = [button];
  button.disabled = true;
  try {
    await action();
  } catch (error) {
    renderSettingsResult(error.message || "処理に失敗しました。", "error");
  } finally {
    for (const item of buttons) {
      item.disabled = false;
    }
  }
}

function createLogEntry(speaker, text, damage = null, reason = null) {
  return { speaker, text, damage, reason };
}

function speakerLabel(speaker) {
  switch (speaker) {
    case "world":
      return "導入";
    case "enemy":
      return "敵";
    case "player":
      return "あなた";
    default:
      return speaker;
  }
}
