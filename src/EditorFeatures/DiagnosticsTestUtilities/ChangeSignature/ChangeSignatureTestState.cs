﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.ChangeSignature;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.VisualBasic;
using Roslyn.Test.Utilities;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.ChangeSignature
{
    internal sealed class ChangeSignatureTestState : IDisposable
    {
        private static readonly TestComposition s_composition = EditorTestCompositions.EditorFeatures.AddParts(typeof(TestChangeSignatureOptionsService));

        private readonly TestHostDocument _testDocument;
        public TestWorkspace Workspace { get; }
        public Document InvocationDocument { get; }
        public AbstractChangeSignatureService ChangeSignatureService { get; }
        public string ErrorMessage { get; private set; }
        public NotificationSeverity ErrorSeverity { get; private set; }

        public static ChangeSignatureTestState Create(string markup, string languageName, ParseOptions parseOptions = null)
        {
            var workspace = languageName == LanguageNames.CSharp
                  ? TestWorkspace.CreateCSharp(markup, composition: s_composition, parseOptions: (CSharpParseOptions)parseOptions)
                  : TestWorkspace.CreateVisualBasic(markup, composition: s_composition, parseOptions: parseOptions, compilationOptions: new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return new ChangeSignatureTestState(workspace);
        }

        public static ChangeSignatureTestState Create(XElement workspaceXml)
        {
            var workspace = TestWorkspace.Create(workspaceXml, composition: s_composition);
            return new ChangeSignatureTestState(workspace);
        }

        public ChangeSignatureTestState(TestWorkspace workspace)
        {
            Workspace = workspace;
            _testDocument = Workspace.Documents.SingleOrDefault(d => d.CursorPosition.HasValue);

            if (_testDocument == null)
            {
                throw new ArgumentException("markup does not contain a cursor position", nameof(workspace));
            }

            InvocationDocument = Workspace.CurrentSolution.GetDocument(_testDocument.Id);
            ChangeSignatureService = InvocationDocument.GetLanguageService<AbstractChangeSignatureService>();
        }

        public TestChangeSignatureOptionsService TestChangeSignatureOptionsService
        {
            get
            {
                return (TestChangeSignatureOptionsService)InvocationDocument.Project.Solution.Workspace.Services.GetRequiredService<IChangeSignatureOptionsService>();
            }
        }

        public ChangeSignatureResult ChangeSignature()
        {
            WpfTestRunner.RequireWpfFact($"{nameof(AbstractChangeSignatureService.ChangeSignature)} currently needs to run on a WPF Fact because it's factored in a way that tries popping up UI in some cases.");

            return ChangeSignatureService.ChangeSignature(
                InvocationDocument,
                _testDocument.CursorPosition.Value,
                (errorMessage, severity) =>
                {
                    this.ErrorMessage = errorMessage;
                    this.ErrorSeverity = severity;
                },
                CancellationToken.None);
        }

        public async Task<ParameterConfiguration> GetParameterConfigurationAsync()
        {
            WpfTestRunner.RequireWpfFact($"{nameof(AbstractChangeSignatureService.ChangeSignature)} currently needs to run on a WPF Fact because it's factored in a way that tries popping up UI in some cases.");

            var context = await ChangeSignatureService.GetContextAsync(InvocationDocument, _testDocument.CursorPosition.Value, restrictToDeclarations: false, CancellationToken.None);
            if (context is ChangeSignatureAnalysisSucceededContext changeSignatureAnalyzedSucceedContext)
            {
                return changeSignatureAnalyzedSucceedContext.ParameterConfiguration;
            }

            throw Roslyn.Utilities.ExceptionUtilities.UnexpectedValue(((CannotChangeSignatureAnalyzedContext)context).CannotChangeSignatureReason.ToString());
        }

        public void Dispose()
        {
            if (Workspace != null)
            {
                Workspace.Dispose();
            }
        }
    }
}
