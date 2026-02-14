namespace SqlScript.Generator.Application;

public sealed class GenerateMigrationSqlHandler
{
    private readonly IMigrationSqlGenerator _generator;

    public GenerateMigrationSqlHandler(IMigrationSqlGenerator generator)
    {
        _generator = generator;
    }

    public void Handle(GenerateMigrationSqlCommand command)
    {
        var sql = _generator.Generate(command);
        Directory.CreateDirectory(Path.GetDirectoryName(command.OutputPath)!);
        File.WriteAllText(command.OutputPath, sql);
    }
}