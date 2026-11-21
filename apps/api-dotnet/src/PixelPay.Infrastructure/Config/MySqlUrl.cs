namespace PixelPay.Infrastructure.Config;

public static class MySqlUrl
{
    public static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "mysql")
            throw new FormatException("DATABASE_URL must use the mysql:// scheme");

        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.IsDefaultPort ? 3306 : uri.Port;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        return $"Server={uri.Host};Port={port};Database={Quote(database)};User ID={Quote(user)};Password={Quote(password)};";
    }

    private static string Quote(string value) =>
        value.IndexOfAny([';', '=', '"', '\'']) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
}
