using System.Data.Common;
using System.Reflection;
using Marten;
using Marten.Services;
using Weasel.Core;
using Weasel.Storage;

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
    private IUnitOfWork? _pendingChanges;
    private RecordingUnitOfWork? _pendingRecorder;

    internal Exception? SaveChangesException { get; set; }

    internal Exception? ExecuteException { get; set; }

    internal int ExecuteResult { get; set; } = 1;

    internal static IDocumentStore Create(out RecordingDocumentSession recorder)
    {
        var session = CreateSession(out recorder);
        return RecordingDocumentStore.Create(Store, session);
    }

    internal static IDocumentSession CreateSession(out RecordingDocumentSession recorder)
    {
        var session = Create<IDocumentSession, RecordingDocumentSession>();
        recorder = (RecordingDocumentSession)(object)session;
        recorder._pendingChanges = DispatchProxy.Create<IUnitOfWork, RecordingUnitOfWork>();
        recorder._pendingRecorder = (RecordingUnitOfWork)(object)recorder._pendingChanges;
        return session;
    }

    private class RecordingDocumentStore : DispatchProxy
    {
        private IDocumentStore? _store;
        private IDocumentSession? _session;

        internal static IDocumentStore Create(IDocumentStore store, IDocumentSession session)
        {
            var proxy = Create<IDocumentStore, RecordingDocumentStore>();
            var recorder = (RecordingDocumentStore)(object)proxy;
            recorder._store = store;
            recorder._session = session;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == "get_Options")
            {
                return _store!.Options;
            }

            if (targetMethod.Name == "LightweightSession")
            {
                return _session;
            }

            if (targetMethod.ReturnType == typeof(void))
            {
                return null;
            }

            if (targetMethod.ReturnType == typeof(ValueTask))
            {
                return ValueTask.CompletedTask;
            }

            throw new NotSupportedException($"The test store does not implement {targetMethod.Name}.");
        }
    }

    internal string[] TakeMutationCalls()
    {
        var calls = _calls
            .Where(call => call is "Insert" or "Update" or "ExecuteAsync" or "SaveChangesAsync" or "Eject")
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

        if (targetMethod.Name == "get_PendingChanges")
        {
            return _pendingChanges;
        }

        if (targetMethod.Name == "Update")
        {
            var document = args![0] is Array { Length: 1 } documents
                ? documents.GetValue(0)!
                : args[0]!;
            _pendingRecorder!.SetDocument(document);
            return null;
        }

        if (targetMethod.Name == "ExecuteAsync" && targetMethod.ReturnType == typeof(Task<int>))
        {
            return ExecuteException is null
                ? Task.FromResult(ExecuteResult)
                : Task.FromException<int>(ExecuteException);
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

    private class RecordingUnitOfWork : DispatchProxy
    {
        private RevisionedUpdateOperation? _operation;

        internal void SetDocument(object document)
            => _operation = new RevisionedUpdateOperation(document);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == "OperationsFor")
            {
                return _operation is null
                    ? Array.Empty<Weasel.Storage.IStorageOperation>()
                    : new Weasel.Storage.IStorageOperation[] { _operation };
            }

            throw new NotSupportedException(
                $"The test unit of work does not implement {targetMethod.Name}.");
        }
    }

    private sealed class RevisionedUpdateOperation(object document) :
        IDocumentStorageOperation,
        IRevisionedOperation
    {
        public object Document { get; } = document;

        public Type DocumentType => Document.GetType();

        public long Revision { get; set; }

        public bool IgnoreConcurrencyViolation { get; set; }

        public void ConfigureCommand(ICommandBuilder builder, IStorageSession session)
            => throw new NotSupportedException();

        public OperationRole Role() => OperationRole.Update;

        public Task PostprocessAsync(
            DbDataReader reader,
            IList<Exception> exceptions,
            CancellationToken token)
            => Task.CompletedTask;

        public IChangeTracker ToTracker(IStorageSession session)
            => throw new NotSupportedException();
    }
}
#pragma warning restore CA1852
