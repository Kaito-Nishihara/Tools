✅ Azure Artifacts に NuGet を push する手順（決定版）
① Feed の v3 URL を確認

Azure DevOps
→ Artifacts
→ 対象 Feed
→ Connect to feed
→ NuGet (dotnet) を選択

出てくる v3 URL をコピーします。

例：

https://pkgs.dev.azure.com/<ORG>/<PROJECT>/_packaging/<FEED>/nuget/v3/index.json


※ /nuget/v2/ は使わない

② 既存 source を確認
dotnet nuget list source


au2485731 が v2 を向いていたら修正。

③ source を v3 に更新（重要）
dotnet nuget update source "au2485731" `
  --source "https://pkgs.dev.azure.com/ORG/PROJECT/_packaging/FEED/nuget/v3/index.json"

④ PAT を作成（まだなら）

Azure DevOps → User Settings → Personal Access Token

権限：

Packaging → Read & Write

⑤ PAT を source に登録
dotnet nuget update source "au2485731" `
  --username "anything" `
  --password "<YOUR_PAT>" `
  --store-password-in-clear-text


※ username は何でもOK
※ PAT が本体

⑥ push 実行

v3でも念のため --api-key 付けるのが安全です

dotnet nuget push .\nupkg\An1.Cli.0.2.0.nupkg `
  --source "au2485731" `
  --api-key "AzureDevOps"