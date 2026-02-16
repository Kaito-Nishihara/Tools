補足：ローカルnupkgを更新するときの流れ

新しい nupkg を作る（バージョン上げるの必須）
```
dotnet pack .\An1.Cli\An1.Cli.csproj -c Release -o .\nupkg
```


更新
```
dotnet tool update An1.Cli --add-source (Resolve-Path .\nupkg) --tool-path .\.tools
```


重要：Version を上げないと update しても変わりません（同じ 0.1.0 のままだと更新されない）