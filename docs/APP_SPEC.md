# App Spec

## Goal

`KotobaColiseum` is a local-first one-battle web game where the player damages an enemy with words.

The app prioritizes:

- one-click startup on Windows with Docker Desktop
- a single `web` service instead of a split worker topology
- local-only OpenAI API key storage
- graceful fallback when OpenAI image generation fails
- a mock lane so UI/E2E can run without a real API key

## App Model

- `web`: ASP.NET Core Minimal API + static files in `wwwroot`
- storage: local filesystem under `app-data`
- orchestration: Docker Compose with one `web` service
- tests: Playwright smoke test in `tests/KotobaColiseum.E2E`

## User Flow

1. Run `start.bat`
2. Open the browser UI
3. Choose `dynamic` or `fixed` battle generation mode
4. Save an OpenAI API key in Settings if dynamic generation should use OpenAI
5. Run connection test
6. Start the battle
7. Attack by text input
8. Optionally use Web Speech API where supported
9. Win by reducing enemy HP to zero

## Settings Storage

Stored in `app-data/settings.json`:

- metadata such as last successful OpenAI connectivity test
- battle generation mode (`dynamic` or `fixed`)

Stored separately in `app-data/secrets/openai.key`:

- the OpenAI API key

## Battle Surface

`POST /api/start-battle` returns:

- `encounter.enemies`
- `worldIntro`
- `enemy`
- `openingLine`
- `enemyImagePrompt`
- `bgImagePrompt`
- `enemyImage`
- `bgImage`
- `provider`
- `generationMode`
- `fellBackToFixed`

`POST /api/attack` returns:

- `damage`
- `reason`
- `enemyLine`
- `animation`
- `provider`

## OpenAI Usage

- dynamic encounter generation uses the OpenAI Responses API with JSON schema output
- attack generation for dynamic enemies also uses structured output
- connection test uses a lightweight authenticated OpenAI API request
- image generation uses the OpenAI images generation endpoint for both the enemy sprite and the battle background
- enemy sprites request transparent backgrounds, while battle backgrounds use opaque renders
- if any dynamic generation call fails, the battle falls back to the fixed enemy mode

## Non-Goals For Today

- multi-enemy campaigns
- persistent save slots
- database storage
- separate frontend build system
- worker orchestration
