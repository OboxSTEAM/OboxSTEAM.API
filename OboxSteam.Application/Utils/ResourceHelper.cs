using System.Reflection;

namespace OboxSteam.Application.Utils;

public static class ResourceHelper
{
    public static string ReadResource(string relativePath, Assembly fromAssembly)
    {
        var assembly = fromAssembly ?? typeof(ResourceHelper).Assembly;
        var str = relativePath.Replace('/', '.').Replace('\\', '.');

        using var manifestResourceStream = assembly.GetManifestResourceStream(assembly.GetName().Name + "." + str);
        if (manifestResourceStream == null)
            throw new IOException("Failed to read manifest resource.");
        using var streamReader = new StreamReader(manifestResourceStream);
        return streamReader.ReadToEnd();
    }

    public static string ReadJsonResource(
        string relativePath,
        Assembly fromAssembly,
        bool stripWhitespace = false)
    {
        return !stripWhitespace
            ? ReadResource(relativePath, fromAssembly)
            : ReadResource(relativePath, fromAssembly).StripJsonWhitespace();
    }

    public static int DateTimeValidate(DateTime startDate, DateTime endDate)
    {
        // Only consider date part, ignore time
        startDate = startDate.Date;
        endDate = endDate.Date;

        if (endDate < startDate)
            throw new ArgumentException("EndDate cannot be earlier than StartDate.");

        return (endDate - startDate).Days + 1;
    }
}
