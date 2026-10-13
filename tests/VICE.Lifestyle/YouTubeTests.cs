using VICE.Lifestyle.Television;

internal static class YouTubeTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string id="M7lc1UVf-VE";
        check(YouTubeRules.VideoId("https://youtu.be/"+id)==id,"Shared YouTube IDs did not remain compatible.");
        check(!TVYouTube.Available&&TVYouTube.Provider is null,"The base TV enabled YouTube without an add-on.");
        Reject(TVYouTube.RequireAvailable,check,"Base TV allowed a missing add-on.");
        var provider=new Provider();var registration=TVYouTube.Register(provider);
        check(!TVYouTube.Available,"An add-on without its companion enabled the source.");
        provider.IsAvailable=true;TVYouTube.RequireAvailable();
        check(TVYouTube.Available&&provider.Created==0,"Availability probing started a browser session.");
        Reject(()=>TVYouTube.Register(new Provider()),check,"Duplicate providers were accepted.");
        registration.Dispose();check(!TVYouTube.Available,"Removing the add-on left YouTube available.");
        using var replacement=TVYouTube.Register(provider);registration.Dispose();
        check(TVYouTube.Available,"Disposing an old registration removed a new registration.");
        replacement.Dispose();
        var saved=new TVDocument{Screens=new(){new(){Id=Guid.NewGuid().ToString(),Media=new(){Kind="youtube",Source=id},Remember=true,Playing=true}},Favorites=new(){new(){Kind="youtube",Source=id}}};
        var restored=TVDocument.Load(saved.SaveJson(),10);
        check(!TVYouTube.Available&&restored.Screens[0].Media.Source==id&&!restored.Screens[0].Playing&&restored.Favorites[0].Source==id,"Removing the add-on broke existing selections or favorites.");
        check(!YouTubeRules.IsId(id+"\n"),"An ID with trailing newline was accepted.");
    }
    private static void Reject(Action action,Action<bool,string> check,string message)
    {try{action();}catch(InvalidOperationException){check(true,message);return;}check(false,message);}
    private sealed class Provider:ITVYouTubeProvider
    {
        public bool IsAvailable{get;set;}
        public string UnavailableReason=>"Companion missing";
        internal int Created;
        public ITVYouTubeSession Create(string videoId,double now){Created++;throw new NotSupportedException();}
    }
}
