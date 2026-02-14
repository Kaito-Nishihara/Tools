namespace SqlScript.Generator.Application;

public interface IMigrationSqlGenerator
{
    string Generate(GenerateMigrationSqlCommand command);
}