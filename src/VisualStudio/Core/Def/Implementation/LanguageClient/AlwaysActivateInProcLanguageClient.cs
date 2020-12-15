﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Experiments;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.LanguageClient
{
    /// <summary>
    /// Language client responsible for handling C# / VB LSP requests in any scenario (both local and codespaces).
    /// This powers "LSP only" features (e.g. cntrl+Q code search) that do not use traditional editor APIs.
    /// It is always activated whenever roslyn is activated.
    /// </summary>
    [ContentType(ContentTypeNames.CSharpContentType)]
    [ContentType(ContentTypeNames.VisualBasicContentType)]
    [Export(typeof(ILanguageClient))]
    [Export(typeof(AlwaysActivateInProcLanguageClient))]
    internal class AlwaysActivateInProcLanguageClient : AbstractInProcLanguageClient
    {
        private readonly DefaultCapabilitiesProvider _defaultCapabilitiesProvider;
        private readonly IGlobalOptionService _globalOptionService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, true)]
        public AlwaysActivateInProcLanguageClient(
            IGlobalOptionService globalOptionService,
            LanguageServerProtocol languageServerProtocol,
            VisualStudioWorkspace workspace,
            IAsynchronousOperationListenerProvider listenerProvider,
            ILspSolutionProvider solutionProvider,
            DefaultCapabilitiesProvider defaultCapabilitiesProvider)
            : base(languageServerProtocol, workspace, diagnosticService: null, listenerProvider, solutionProvider, diagnosticsClientName: null)
        {
            _globalOptionService = globalOptionService;
            _defaultCapabilitiesProvider = defaultCapabilitiesProvider;
        }

        public override string Name
            => ServicesVSResources.CSharp_Visual_Basic_Language_Server_Client;

        protected internal override VSServerCapabilities GetCapabilities()
        {
            var serverCapabilities = new VSServerCapabilities();

            // If the LSP editor feature flag is enabled advertise support for LSP features here so they are available locally and remote.
            if (Workspace.Services.GetRequiredService<IExperimentationService>().IsExperimentEnabled(VisualStudioWorkspaceContextService.LspEditorFeatureFlagName))
            {
                serverCapabilities = _defaultCapabilitiesProvider.GetCapabilities();
            }

            serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions
            {
                Change = TextDocumentSyncKind.Incremental,
                OpenClose = true,
            };
            serverCapabilities.SupportsDiagnosticRequests = this.Workspace.IsPullDiagnostics(InternalDiagnosticsOptions.NormalDiagnosticMode);
            serverCapabilities.DisableGoToWorkspaceSymbols = true;
            serverCapabilities.WorkspaceSymbolProvider = true;

            return serverCapabilities;
        }
    }
}
