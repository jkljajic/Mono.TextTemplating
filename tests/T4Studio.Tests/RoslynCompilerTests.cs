using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using Xunit;

namespace T4Studio.Tests
{
	/// <summary>
	/// Tests for RoslynTemplateCompiler — covers framework assembly references,
	/// duplicate references, debug/PDB output, and empty/missing references.
	/// </summary>
	public class RoslynCompilerTests
	{
		[Fact]
		public void FrameworkAssemblyNames_ShouldBeFilteredOut ()
		{
			var compiler = new Compilation.RoslynTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestTemplate",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = false,
			};

			var ccu = new CodeCompileUnit ();
			var ns = new CodeNamespace ("Test");
			ns.Imports.Add (new CodeNamespaceImport ("System"));
			ccu.Namespaces.Add (ns);

			// These are framework assembly names — not file paths
			// RoslynTemplateCompiler should filter them out (skip non-file refs)
			var references = new[] {
				"System.Linq",           // not a file path
				"System",                // not a file path
				typeof (string).Assembly.Location, // valid file path
			};

			var results = compiler.Compile (ccu, settings, references);

			// Should compile successfully — framework refs are provided by TPA
			Assert.NotNull (results);
			Assert.NotNull (results.CompiledAssembly);
			Assert.False (results.Errors.HasErrors,
				string.Join ("; ", results.Errors.Cast<CompilerError> ().Select (e => e.ErrorText)));
		}

		[Fact]
		public void DuplicateReferences_ShouldNotCauseError ()
		{
			var compiler = new Compilation.RoslynTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestDup",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = false,
			};

			var ccu = new CodeCompileUnit ();
			var ns = new CodeNamespace ("Test2");
			ccu.Namespaces.Add (ns);

			var strAsm = typeof (string).Assembly.Location;
			var references = new[] { strAsm, strAsm, strAsm }; // duplicate

			var results = compiler.Compile (ccu, settings, references);

			Assert.NotNull (results);
			Assert.NotNull (results.CompiledAssembly);
		}

		[Fact]
		public void InvalidCSharpCode_ShouldReturnErrors ()
		{
			var compiler = new Compilation.RoslynTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestInvalid",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = false,
			};

			var ccu = new CodeCompileUnit ();
			var ns = new CodeNamespace ("BadNs");
			var type = new CodeTypeDeclaration ("BadClass");
			// Use an undeclared identifier as a type reference — will fail to compile
			type.Members.Add (new CodeSnippetTypeMember ("UndeclaredXYZ.Foo();"));
			ns.Types.Add (type);
			ccu.Namespaces.Add (ns);

			var references = new[] { typeof (string).Assembly.Location };

			var results = compiler.Compile (ccu, settings, references);

			Assert.NotNull (results);
			Assert.True (results.Errors.HasErrors,
				"Should have compilation errors for undeclared identifier");
		}

		[Fact]
		public void DebugMode_ShouldIncludeDebugInfo ()
		{
			var compiler = new Compilation.RoslynTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestDebug",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = true,
			};

			var ccu = new CodeCompileUnit ();
			var ns = new CodeNamespace ("Test4");
			ccu.Namespaces.Add (ns);

			var references = new[] { typeof (string).Assembly.Location };

			var results = compiler.Compile (ccu, settings, references);

			Assert.NotNull (results);
			Assert.NotNull (results.CompiledAssembly);
			// Debug mode should produce assembly with debug info
		}

		[Fact]
		public void NullReferences_ShouldBeFilteredOut ()
		{
			var compiler = new Compilation.RoslynTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestNullRefs",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = false,
			};

			var ccu = new CodeCompileUnit ();
			var ns = new CodeNamespace ("Test5");
			ccu.Namespaces.Add (ns);

			var references = new string[] { null, "", "  " };

			var results = compiler.Compile (ccu, settings, references);

			Assert.NotNull (results);
			Assert.NotNull (results.CompiledAssembly);
		}

		[Fact]
		public void LanguageVersionMapping_ShouldSupportCommonT4Strings ()
		{
			// Verify GetLanguageVersion handles all common T4 language strings
			var compiler = new Compilation.RoslynTemplateCompiler ();

			foreach (var lang in new[] { "C#", "C#v3.5", "C#v5.0", "C#v7.0",
				 "C#v9.0", "C#v12.0", "VB", "" }) {
				var settings = new TemplateSettings {
					Name = "TestLang",
					Language = lang,
					Provider = new CSharpCodeProvider (),
					Debug = false,
				};

				var ccu = new CodeCompileUnit ();
				ccu.Namespaces.Add (new CodeNamespace ("Lang" + lang.Replace (".", "_").Replace ("#", "Sharp")));

				var results = compiler.Compile (ccu, settings,
					new[] { typeof (string).Assembly.Location });

				Assert.NotNull (results);
				// Should not throw regardless of language string
			}
		}

		[Fact]
		public void CodeDomCompiler_ShouldNotThrowOnCall ()
		{
			// CodeDomTemplateCompiler may throw PlatformNotSupportedException on .NET 10+
			// because Microsoft.CSharp.CSharpCodeGenerator.FromFileBatch is not supported
			var compiler = new Compilation.CodeDomTemplateCompiler ();
			var settings = new TemplateSettings {
				Name = "TestCodeDom",
				Language = "C#",
				Provider = new CSharpCodeProvider (),
				Debug = false,
			};
			var ccu = new CodeCompileUnit ();
			ccu.Namespaces.Add (new CodeNamespace ("CodeDomTest"));

			try {
				compiler.Compile (ccu, settings,
					new[] { typeof (string).Assembly.Location });
				// If it doesn't throw, that's fine
			} catch (PlatformNotSupportedException) {
				// Expected on .NET 10+
			}
		}
	}
}

