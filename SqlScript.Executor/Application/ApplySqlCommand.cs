using System;
using System.Collections.Generic;
using System.Text;

namespace SqlScript.Executor.Application;

public sealed record ApplySqlCommand(
    string FunctionUrl,
    string FunctionKey,
    string Context,
    string BaseMigrationId,
    string TargetMigrationId,
    string Sql,
    string Sha256,
    bool DryRun,
    TimeSpan Timeout);
