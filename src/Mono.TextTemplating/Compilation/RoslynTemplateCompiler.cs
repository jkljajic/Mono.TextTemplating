// 
// RoslynTemplateCompiler.cs
//  
// Copyright (c) 2025 Mono.TextTemplating Contributors
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Mono.TextTemplating.Compilation
{
	/// <summary>
	/// Compiles templates using Roslyn (Microsoft.CodeAnalysis).
	/// This backend supports modern C# features and is the default for .NET 6+.
	/// </summary>
	public class RoslynTemplateCompiler : ITemplateCompiler
	{
		public CompilerResults Compile (CodeCompileUnit compileUnit, TemplateSettings settings, IEnumerable<string> references)
		{
			// 1. Convert CodeCompileUnit → C# source text using CodeDOM
			string sourceText;
			using (var writer = new StringWriter ()) {
				var options = new CodeGeneratorOptions ();
				settings.Provider.GenerateCodeFromCompileUnit (compileUnit, writer, options);
				sourceText = writer.ToString ();
			}

			// 2. Parse into Roslyn syntax tree with C# language version based on settings
			var languageVersion = GetLanguageVersion (settings.Language);
			var parseOptions = new CSharpParseOptions (languageVersion);
			var syntaxTree = CSharpSyntaxTree.ParseText (sourceText, parseOptions);

			// 3. Create compilation — only add references that are valid file paths
			var metadataReferences = references
				.Where (r => !string.IsNullOrEmpty (r) && File.Exists (r))
				.Select (r => MetadataReference.CreateFromFile (r))
				.ToList ();

			// Add core runtime references (includes System.Linq, System, etc.)
			var trustedAssemblies = ((string) AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split (Path.PathSeparator);
			var runtimeRefs = trustedAssemblies
				.Where (p => !string.IsNullOrEmpty (p) && File.Exists (p))
				.Select (p => MetadataReference.CreateFromFile (p));

			metadataReferences.AddRange (runtimeRefs);

			var compilation = CSharpCompilation.Create (
				settings.Name ?? "GeneratedTemplate",
				new[] { syntaxTree },
				metadataReferences.Distinct (MetadataReferenceEqualityComparer.Instance),
				new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary)
					.WithOptimizationLevel (settings.Debug ? OptimizationLevel.Debug : OptimizationLevel.Release));

			// 4. Emit to memory stream
			using var ms = new MemoryStream ();
			using var pdbStream = settings.Debug ? new MemoryStream () : null;

			EmitResult emitResult;
			if (settings.Debug) {
				emitResult = compilation.Emit (ms, pdbStream);
			} else {
				emitResult = compilation.Emit (ms);
			}

			// 5. Map Roslyn diagnostics → CompilerErrorCollection
			var errors = new CompilerErrorCollection ();
			foreach (var diagnostic in emitResult.Diagnostics) {
				if (diagnostic.Severity == DiagnosticSeverity.Warning || diagnostic.Severity == DiagnosticSeverity.Error) {
					var error = new CompilerError (
						diagnostic.Location.SourceTree?.FilePath ?? "",
						diagnostic.Location.GetLineSpan ().StartLinePosition.Line + 1,
						diagnostic.Location.GetLineSpan ().StartLinePosition.Character + 1,
						diagnostic.Id,
						diagnostic.GetMessage ());
					error.IsWarning = diagnostic.Severity == DiagnosticSeverity.Warning;
					errors.Add (error);
				}
			}

			if (!emitResult.Success) {
				var result = new CompilerResults (null);
				foreach (CompilerError err in errors)
					result.Errors.Add (err);
				return result;
			}

			// 6. Load the compiled assembly
			ms.Seek (0, SeekOrigin.Begin);
			var assembly = System.Reflection.Assembly.Load (ms.ToArray ());

			var compilerResults = new CompilerResults (new TempFileCollection ()) {
				CompiledAssembly = assembly,
			};
			foreach (CompilerError err in errors)
				compilerResults.Errors.Add (err);
			return compilerResults;
		}

		static LanguageVersion GetLanguageVersion (string language)
		{
			if (string.IsNullOrEmpty (language))
				return LanguageVersion.Default;

			// Support common T4 language strings
			return language switch {
				"C#" or "C#v3.5" or "C#v4.0" => LanguageVersion.CSharp7_3,
				"C#v5.0" => LanguageVersion.CSharp5,
				"C#v6.0" => LanguageVersion.CSharp6,
				"C#v7.0" => LanguageVersion.CSharp7,
				"C#v7.1" => LanguageVersion.CSharp7_1,
				"C#v7.2" => LanguageVersion.CSharp7_2,
				"C#v7.3" => LanguageVersion.CSharp7_3,
				"C#v8.0" => LanguageVersion.CSharp8,
				"C#v9.0" => LanguageVersion.CSharp9,
				"C#v10.0" => LanguageVersion.CSharp10,
				"C#v11.0" => LanguageVersion.CSharp11,
				"C#v12.0" => LanguageVersion.CSharp12,
				"C#v13.0" => LanguageVersion.CSharp13,
				_ => LanguageVersion.Latest,
			};
		}

		/// <summary>
		/// Custom equality comparer for MetadataReference to allow deduplication.
		/// </summary>
		class MetadataReferenceEqualityComparer : IEqualityComparer<MetadataReference>
		{
			public static readonly MetadataReferenceEqualityComparer Instance = new ();

			public bool Equals (MetadataReference x, MetadataReference y)
			{
				if (x == y) return true;
				if (x == null || y == null) return false;
				return string.Equals (x.Display, y.Display, StringComparison.OrdinalIgnoreCase);
			}

			public int GetHashCode (MetadataReference obj)
			{
				return obj?.Display?.ToUpperInvariant ().GetHashCode () ?? 0;
			}
		}
	}
}
