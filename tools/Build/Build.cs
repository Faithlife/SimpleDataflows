return BuildRunner.Execute(args, build =>
{
	var gitLogin = new GitLoginInfo("faithlifebuildbot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? "");

	build.AddDotNetTargets(
		new DotNetBuildSettings
		{
			NuGetApiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY"),
			DocsSettings = new DotNetDocsSettings
			{
				GitLogin = gitLogin,
				GitAuthor = new GitAuthorInfo("Faithlife Build Bot", "faithlifebuildbot@users.noreply.github.com"),
				GitRepositoryUrl = "https://github.com/Faithlife/SimpleDataflows.git",
				GitBranchName = "docs",
				TargetDirectory = "",
				SourceCodeUrl = "https://github.com/Faithlife/SimpleDataflows/tree/master/src",
			},
			PackageSettings = new DotNetPackageSettings
			{
				GitLogin = gitLogin,
				PushTagOnPublish = x => $"v{x.Version}",
			},
		});
});
