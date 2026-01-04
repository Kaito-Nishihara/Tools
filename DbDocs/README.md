# DbDocs 🔧

**DbDocs** は SQL Server 向けのデータベース定義書（HTML / PDF / Markdown / DBML）を自動生成するツールです。テーブル・カラム・インデックス・外部キー情報を収集して、日本語のテーブル定義書フォーマットで出力します。

---

## 特長 ✅

- HTML（book.html）にまとめた読みやすい定義書を生成
- 任意で PDF（Playwright を利用）を出力可能
- Markdown（index.md / tables/*.md）や DBML（schema.dbml）を同時生成

---

## 前提条件 ⚙️

- .NET SDK（対応バージョンはプロジェクトのターゲットに合わせてください）
- SQL Server へ接続可能な接続文字列
- PDF を生成する場合は Playwright のブラウザが必要（chromium 等）

---

## ビルド方法 🔧

```powershell
dotnet build DbDocs
```

Playwright のブラウザを手動でインストールする場合（パスはビルドターゲットに応じて変わる可能性があります）:

```powershell
pwsh .\DbDocs\bin\Debug\net10.0\playwright.ps1 install chromium
```

---

## 実行例 — 基本的な使い方 ▶️

必須: `--connection`（接続文字列）

```powershell
# HTML を出力
dotnet run --project DbDocs -- --connection "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" --out ".\db-docs" --title "NK Admin DB 定義書"

# HTML + PDF を出力
dotnet run --project DbDocs -- --connection "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" --out ".\db-docs" --title "NK Admin DB 定義書" --pdf
```

### CLI オプション

- `--connection <CS>` : 接続文字列（必須）
- `--out <DIR>` : 出力ディレクトリ（デフォルト: ./db-docs）
- `--title <TITLE>` : ドキュメントのタイトル（デフォルト: DB 定義書）
- `--pdf` : PDF を生成するフラグ
- `--system <NAME>` : メタ情報（System Name、デフォルト: NK Admin）
- `--rdbms <NAME>` : RDBMS 名（デフォルト: SQL Server）
- `--author <NAME>` : 作成者（デフォルト: 実行ユーザー）
- `--created-date <yyyy/MM/dd>` : 作成日（デフォルト: 当日）

---

## スクリプトでの利用（PowerShell）

付属のスクリプト `scripts\generate-db-docs.ps1` を使うと、Markdown 出力や DBML 生成を簡単に行えます:

```powershell
pwsh .\scripts\generate-db-docs.ps1 \
  -ConnectionString "Server=localhost;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  -OutDir ".\db-docs\md"
```

このスクリプトは `index.md`, `tables/*.md`, `schema.dbml` を生成します。

---

## 出力ファイルについて 📁

- `book.html` : HTML 定義書（全体）
- `db-spec.pdf` : PDF（`--pdf` 指定時）
- `index.md` , `tables/*.md` : Markdown の表形式ドキュメント
- `schema.dbml` : dbdiagram などで利用できる DBML

---

