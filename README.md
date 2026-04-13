# KotobaColiseum

`KotobaColiseum` is a local-first one-battle web game. The player defeats `Yakisoba-eating Wannabe Man` by attacking with words.

[日本語 README](README.ja.md)

## Supported Path

Primary supported path today:

- Windows
- Docker Desktop
- `start.bat`

## Quick Start

```powershell
.\start.bat
```

The launcher:

1. checks Docker Desktop
2. creates `.env` from `.env.example`
3. runs `docker compose up --build -d`
4. waits for `/health`
5. opens the app in a browser window

## What You Can Do

- save an OpenAI API key from the Settings panel
- switch battle generation mode between `dynamic` and `fixed`
- test connectivity
- start a dynamic one-enemy encounter or a fixed `焼きそば食べたいマン` battle
- generate a pixel-art enemy sprite and a pixel-art battle background with OpenAI image generation
- attack with text
- optionally use Web Speech API when the browser supports it
- watch a lightweight generation progress bar during battle setup
- see intros and enemy lines revealed with a typewriter effect
- keep using the app even if image generation fails or dynamic generation falls back to fixed

## Local Storage

- metadata: `app-data/settings.json`
- secret: `app-data/secrets/openai.key`

The key is never baked into the build and is not read from `.env`.
The selected battle generation mode is also stored locally in `settings.json`.

## Project Layout

```text
KotobaColiseum/
  docker/
  configs/
  docs/
  scripts/
  tests/
  web/
  .env.example
  docker-compose.yml
  start.bat
  stop.bat
```

## E2E

```powershell
.\scripts\test-e2e.ps1
```

The E2E flow uses mock mode so it can run without a real OpenAI API key.

## GitHub CI / Release

- `main` push and pull request: run `dotnet build` and `BattleServiceTests`
- tag push like `v0.1.0`: build the release image, push it to `ghcr.io/amano0406/kotobacoliseum`, and create a GitHub Release automatically
