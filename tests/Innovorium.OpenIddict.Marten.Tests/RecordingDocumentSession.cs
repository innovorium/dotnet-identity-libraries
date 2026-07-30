using System.Reflection;
using Marten;
using Npgsql;

namespace Innovorium.OpenIddict.Marten.Tests;

#pragma warning disable CA1852 // DispatchProxy generates a runtime subtype.
internal class RecordingDocumentSession : DispatchProxy
{
    private static readonly IDocumentStore Store = DocumentStore.For(options =>
    {
        options.Connection("Host=localhost;Database=unused;Username=unused");
        MartenOpenIddictSchema.Configure(options);
    });

    private readonly List<string> _calls = [];

    internal Exception? SaveChangesException { get; set; }

    internal Exception? ExecuteException { get; set; }

    internal int ExecuteResult { get; set; } = 1;

    internal bool CheckExistsResult { get; set; } = true;

    internal string? LastCommandText { get; private set; }

    internal IReadOnlyDictionary<string, object?> LastCommandParameters { get; private set; }
        = new Dictionary<string, object?>();

    internal static IDocumentSession Create(out RecordingDocumentSession recorder)
    {
        var session = Create<IDocumentSession, RecordingDocumentSession>();
        recorder = (RecordingDocumentSession)(object)session;
        return session;
    }

    internal string[] TakeMutationCalls()
    {
        var calls = _calls
            .Where(call => call is "Insert" or "UpdateRevision" or "ExecuteAsync" or "SaveChangesAsync" or "Eject")
            .ToArray();
        _calls.Clear();
        return calls;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        _calls.Add(targetMethod.Name);

        if (targetMethod.Name == "get_DocumentStore")
        {
            return Store;
        }

        if (targetMethod.Name == "ExecuteAsync" && targetMethod.ReturnType == typeof(Task<int>))
        {
            var command = AssertCommand(args);
            LastCommandText = command.CommandText;
            LastCommandParameters = command.Parameters
                .Cast<NpgsqlParameter>()
                .ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value);

            return ExecuteException is null
                ? Task.FromResult(ExecuteResult)
                : Task.FromException<int>(ExecuteException);
        }

        if (targetMethod.Name == "CheckExistsAsync" && targetMethod.ReturnType == typeof(Task<bool>))
        {
            return Task.FromResult(CheckExistsResult);
        }

        if (targetMethod.ReturnType == typeof(Task))
        {
            return SaveChangesException is null
                ? Task.CompletedTask
                : Task.FromException(SaveChangesException);
        }

        if (targetMethod.ReturnType == typeof(ValueTask))
        {
            return ValueTask.CompletedTask;
        }

        if (targetMethod.ReturnType == typeof(void))
        {
            return null;
        }

        throw new NotSupportedException($"The test session does not implement {targetMethod.Name}.");
    }

    private static NpgsqlCommand AssertCommand(object?[]? args)
        => args is [NpgsqlCommand command, CancellationToken]
            ? command
            : throw new InvalidOperationException("Expected a PostgreSQL command and cancellation token.");
}
#pragma warning restore CA1852
