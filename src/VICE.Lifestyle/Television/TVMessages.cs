namespace VICE.Lifestyle.Television;

public sealed class TVCommand
{
    public string Token { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = "sync";
    public string Screen { get; set; } = "";
    public MediaSelection? Media { get; set; }
    public double Position { get; set; }
    public float Volume { get; set; }
    public bool Remember { get; set; }
    public string Text { get; set; } = "";
    public string Operation { get; set; } = "";
    public int Season { get; set; }
    public string Hash { get; set; } = "";
    public string TransferId { get; set; } = "";
    public int Length { get; set; }
    public int Index { get; set; }
    public byte[]? Data { get; set; }
}

public sealed class TVMessage
{
    public string Type { get; set; } = "reply";
    public string Token { get; set; } = "";
    public bool Success { get; set; } = true;
    public string Text { get; set; } = "";
    public string Json { get; set; } = "";
    public double HostTime { get; set; }
    public List<ScreenState> Screens { get; set; } = new();
    public List<string> AllIds { get; set; } = new();
    public List<MediaSelection> Favorites { get; set; } = new();
    public string Hash { get; set; } = "";
    public string TransferId { get; set; } = "";
    public int Length { get; set; }
    public int Index { get; set; }
    public byte[]? Data { get; set; }
}
