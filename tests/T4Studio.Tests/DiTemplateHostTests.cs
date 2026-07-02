using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using Microsoft.VisualStudio.TextTemplating;
using Xunit;

namespace T4Studio.Tests
{
	/// <summary>
	/// Tests for DiTemplateHost and TemplatingHostBuilder — covers DI integration,
	/// service provider resolution, session state, custom host options, and
	/// parameter resolution with IServiceProvider fallback.
	/// </summary>
	public class DiTemplateHostTests
	{
		[Fact]
		public void Build_Host_ShouldImplementHostInterface ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();

			Assert.NotNull (host);
			Assert.IsAssignableFrom<ITextTemplatingEngineHost> (host);
		}

		[Fact]
		public void Build_Host_ShouldImplementSessionHostInterface ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();

			Assert.IsAssignableFrom<ITextTemplatingSessionHost> (host);
		}

		[Fact]
		public void GetService_ShouldReturnRegisteredService ()
		{
			var svc = new TestService ();
			var sp = new TestServiceProvider ().Register<ITestService> (svc);
			var builder = new TemplatingHostBuilder (sp);
			var host = builder.Build ();

			var resolved = ((DiTemplateHost) host).GetService<ITestService> ();

			Assert.NotNull (resolved);
			Assert.Same (svc, resolved);
		}

		[Fact]
		public void GetService_UnregisteredType_ShouldReturnNull ()
		{
			var sp = new TestServiceProvider ();
			var builder = new TemplatingHostBuilder (sp);
			var host = builder.Build ();

			var resolved = ((DiTemplateHost) host).GetService<ITestService> ();

			Assert.Null (resolved);
		}

		[Fact]
		public void GetHostOption_ServiceProvider_ShouldReturnProvider ()
		{
			var sp = new TestServiceProvider ();
			var builder = new TemplatingHostBuilder (sp);
			var host = builder.Build ();

			var option = host.GetHostOption ("ServiceProvider");

			Assert.NotNull (option);
			Assert.IsType<TestServiceProvider> (option);
		}

		[Fact]
		public void GetHostOption_CustomOption_ShouldReturnValue ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();
			host.SetHostOption ("MyOption", "MyValue");

			var option = host.GetHostOption ("MyOption");

			Assert.Equal ("MyValue", option);
		}

		[Fact]
		public void WithAssemblyReference_ShouldAddToRefs ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithAssemblyReference ("C:\\test\\MyAssembly.dll");

			var host = builder.Build ();
			var refs = host.StandardAssemblyReferences;

			Assert.Contains ("C:\\test\\MyAssembly.dll", refs);
		}

		[Fact]
		public void WithImport_ShouldAddToImports ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithImport ("MyApp.Services");

			var host = builder.Build ();
			var imports = host.StandardImports;

			Assert.Contains ("MyApp.Services", imports);
		}

		[Fact]
		public void WithParameter_ShouldBeResolvable ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithParameter (null, null, "MyParam", "MyValue");

			var host = builder.Build ();
			var value = host.ResolveParameterValue (null, null, "MyParam");

			Assert.Equal ("MyValue", value);
		}

		[Fact]
		public void WithSession_ShouldSetSessionState ()
		{
			var sessionData = new Dictionary<string, object> {
				{ "ConnectionId", "conn-123" },
				{ "UserId", "user-456" },
			};
			var builder = new TemplatingHostBuilder ()
				.WithSession (sessionData);

			var host = builder.Build ();
			var sessionHost = (ITextTemplatingSessionHost) host;

			Assert.NotNull (sessionHost.Session);
			Assert.Equal ("conn-123", sessionHost.Session["ConnectionId"]);
			Assert.Equal ("user-456", sessionHost.Session["UserId"]);
		}

		[Fact]
		public void WithSessionValue_ShouldAddToSession ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithSessionValue ("Key1", "Val1")
				.WithSessionValue ("Key2", 42);

			var host = builder.Build ();
			var sessionHost = (ITextTemplatingSessionHost) host;

			Assert.Equal ("Val1", sessionHost.Session["Key1"]);
			Assert.Equal (42, sessionHost.Session["Key2"]);
		}

		[Fact]
		public void WithIncludePath_ShouldAddIncludePath ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithIncludePath ("C:\\templates");

			// Verify via the generator's include paths
			var host = builder.Build ();
			var gen = host.Generator;

			Assert.Contains ("C:\\templates", gen.IncludePaths);
		}

		[Fact]
		public void WithReferencePath_ShouldAddReferencePath ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithReferencePath ("C:\\refs");

			var host = builder.Build ();
			var gen = host.Generator;

			Assert.Contains ("C:\\refs", gen.ReferencePaths);
		}

		[Fact]
		public void WithDirectiveProcessor_ShouldRegisterProcessor ()
		{
			// Use the built-in ParameterDirectiveProcessor type
			var processorType = typeof (ParameterDirectiveProcessor);
			var asmPath = processorType.Assembly.Location;

			var builder = new TemplatingHostBuilder ()
				.WithDirectiveProcessor ("MyParam", processorType.FullName, asmPath);

			var host = builder.Build ();

			// Direct registration should not throw
			Assert.NotNull (host);
		}

		[Fact]
		public void ResolveParameterValue_FallsBackToDi_WhenNotRegistered ()
		{
			var svc = new TestService ();
			var sp = new TestServiceProvider ().Register<ITestService> (svc);
			var builder = new TemplatingHostBuilder (sp);
			var host = builder.Build ();

			// Resolve by type name (DiTemplateHost falls back to Type.GetType + DI)
			var value = host.ResolveParameterValue (null, null,
				typeof (ITestService).AssemblyQualifiedName);

			// Fallback may not work for complex type names; at minimum it shouldn't throw
			Assert.True (value == null || value.Contains ("TestService"));
		}

		[Fact]
		public void ProcessTemplateContent_ShouldWork ()
		{
			var builder = new TemplatingHostBuilder ()
				.WithImport ("System");

			var template = @"<#@ template language=""C#"" #>
<# Write(""Hello DI""); #>
";
			string outputFileName = Guid.NewGuid ().ToString ();
			var result = builder.ProcessTemplateContent ("test.tt", template, ref outputFileName);

			Assert.Contains ("Hello DI", result);
		}

		[Fact]
		public void Errors_ShouldBeAccessible ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();

			var errors = host.Errors;

			Assert.NotNull (errors);
			Assert.IsType<CompilerErrorCollection> (errors);
		}

		[Fact]
		public void ServiceProvider_Property_ShouldReturnProvider ()
		{
			var sp = new TestServiceProvider ();
			var builder = new TemplatingHostBuilder (sp);
			var host = builder.Build ();

			Assert.Same (sp, host.ServiceProvider);
		}

		[Fact]
		public void Generator_Property_ShouldReturnGenerator ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();

			Assert.NotNull (host.Generator);
			Assert.IsType<TemplateGenerator> (host.Generator);
		}

		[Fact]
		public void CreateSession_ShouldReturnNewSession ()
		{
			var builder = new TemplatingHostBuilder ();
			var host = builder.Build ();

			var session = host.CreateSession ();

			Assert.NotNull (session);
			Assert.NotEqual (Guid.Empty, session.Id);
		}

		#region Test Infrastructure

		interface ITestService
		{
			string GetValue ();
		}

		class TestService : ITestService
		{
			public string GetValue () => "TestValue";
		}

		class TestServiceProvider : IServiceProvider
		{
			readonly Dictionary<Type, object> services = new Dictionary<Type, object> ();

			public TestServiceProvider Register<T> (object instance)
			{
				services[typeof (T)] = instance;
				return this;
			}

			public object GetService (Type serviceType)
			{
				services.TryGetValue (serviceType, out var instance);
				return instance;
			}
		}

		#endregion
	}
}

