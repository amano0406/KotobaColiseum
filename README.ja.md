# KotobaColiseum / ことばコロシアム

`KotobaColiseum` は、言葉で敵を削るローカル Web ゲームです。  
今日の構成は `TimelineForAudio` の起動導線とリポジトリ雰囲気を参考にしつつ、`web` 1 サービスに縮小しています。

[English README](README.md)

## Public Release Status

今日の主目的は次の 3 つです。

- Windows + Docker Desktop で一発起動しやすいこと
- OpenAI API key を画面からローカル保存できること
- バトル開始から勝利まで 1 体分を安定して遊べること

## このアプリがやっていること

このアプリは、`焼きそば食べたいマン` を相手に、プレイヤーが言葉で精神ダメージを与える軽い RPG 風ゲームです。

内部では主に次のことを行います。

1. 設定画面から OpenAI API key をローカル保存します
2. `fixed` / `dynamic` のバトル生成モードをローカル保存します
3. `dynamic` では `start battle` で敵設定、導入文、初手セリフ、画像プロンプトを生成します
4. 敵画像は透過背景のドット絵風、背景画像はドット絵風の戦闘背景として生成します
5. `attack` でプレイヤー発言を判定し、ダメージと敵セリフを返します
6. 敵には `species` があり、HP は毎回少し変動します
7. バトル開始中は進捗バーを表示し、導入文と敵セリフはタイピング風に表示します
8. dynamic 生成が失敗したら固定敵モードへ自動フォールバックします

## クイックスタート

Windows:

```powershell
.\start.bat
```

停止:

```powershell
.\stop.bat
```

## 起動後の流れ

1. ブラウザでトップ画面を開く
2. 右側の `OpenAI 設定` で API key を保存する
3. `バトル生成モード` を `dynamic` または `fixed` で選んで保存する
4. `疎通確認` を押す
5. `ゲーム開始` を押す
6. テキスト入力、必要なら音声入力で攻撃する
7. HP を 0 にして勝利する

## 必要なもの

- Windows
- Docker Desktop
- 初回イメージ取得用のネットワーク
- OpenAI API key

補足:

- API key 未設定でもアプリ自体は起動します
- 未設定時は mock mode で導線確認できます
- 画像生成が失敗してもゲーム本体は続行します

## 設定保存

保存先:

- メタデータ: `app-data/settings.json`
- API key: `app-data/secrets/openai.key`

`settings.json` にはバトル生成モードも保存します。

このキーは `.env` には入れません。`docker-compose.yml` にも埋め込みません。

## ディレクトリ構成

```text
KotobaColiseum/
  docker/
    web.Dockerfile
  configs/
    runtime.defaults.json
    local.json
    docker.json
  docs/
    APP_SPEC.md
  scripts/
    open-app-window.ps1
    test-e2e.ps1
  tests/
    KotobaColiseum.E2E/
  web/
    KotobaColiseum.Web/
  .env.example
  docker-compose.yml
  start.bat
  stop.bat
```

## E2E スモークテスト

```powershell
.\scripts\test-e2e.ps1
```

このスクリプトは:

1. `web` と `tests` を build
2. Playwright の Chromium を入れる
3. ローカルで `web` を起動
4. mock mode で E2E を流す

## 補足

- OpenAI の本文生成は JSON schema を使った構造化出力を優先しています
- dynamic モードでは敵キャラ生成自体も JSON schema で行います
- 画像生成は OpenAI で行い、失敗したらプレースホルダーを返します
- UI では日本語対応のドット風フォント `DotGothic16` を同梱して使っています
- 将来の複数敵対応を見越して `encounter.enemies` を返しますが、今日は敵 1 体固定です

## GitHub CI / Release

- `main` への push と pull request では `dotnet build` と `BattleServiceTests` を実行します
- `v0.1.0` のようなタグ push では release 用 Docker image を build し、`ghcr.io/amano0406/kotobacoliseum` へ publish し、GitHub Release も自動作成します
