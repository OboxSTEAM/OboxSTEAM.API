namespace OboxSteam.API.Architecture;

public static class EnvFileLoader
{
    /// <summary>
    /// Loads variables from the first <c>.env</c> file found while walking up from the current directory.
    /// Existing environment variables are not overwritten.
    /// </summary>
    public static void LoadFromSolutionRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var envFile = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(envFile))
            {
                directory = directory.Parent;
                continue;
            }

            foreach (var rawLine in File.ReadAllLines(envFile))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }

            return;
        }
    }
}
