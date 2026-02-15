using System;
using System.Collections.Generic;
using System.Text;

namespace SqlScript.Executor.Common;

public sealed record CliOptions(
    string FunctionUrl,
    string SqlPath,
    string Context,
    string BaseMigrationId,
    string TargetMigrationId,
    bool DryRun,
    string? FunctionKey,
    int TimeoutSeconds);
