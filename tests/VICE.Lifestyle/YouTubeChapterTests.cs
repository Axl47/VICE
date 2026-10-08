using VICE.Lifestyle.Television;

internal static class YouTubeChapterTests
{
    internal static void Run(Action<bool,string> check)
    {
        const string id="WJNppjUrmxM";
        var page=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","youtube-chapters.html"));
        var chapters=YouTubeChapters.ParsePage(page,id);
        check(chapters.Count==3&&chapters[1].Start==133&&chapters[1].Title=="Intro"&&chapters[2].Title=="Talking to Shorekeeper","Known YouTube player chapters did not decode.");
        check(YouTubeChapters.ParsePage(page.Replace("DESCRIPTION_CHAPTERS","AUTO_CHAPTERS"),id).Count==3,"Automatic player chapters did not decode.");
        check(YouTubeChapters.ParsePage(page.Replace("DESCRIPTION_CHAPTERS","OTHER_TIMESTAMPS"),id).Count==0,"Unrelated timestamps became chapters.");
        check(YouTubeChapters.ParsePage(page.Replace("\"timeRangeStartMillis\":133000","\"timeRangeStartMillis\":0"),id).Count==2,"Duplicate chapter boundaries were retained.");
        check(YouTubeChapters.ParsePage(page.Replace("\"timeRangeStartMillis\":133000","\"timeRangeStartMillis\":-1").Replace("\"timeRangeStartMillis\":199000","\"timeRangeStartMillis\":604800000"),id).Count==1,"Invalid or out-of-bounds chapter times were retained.");
        check(YouTubeChapters.ParsePage(page.Replace("\"timeRangeStartMillis\":133000","\"timeRangeStartMillis\":\"133000\""),id).Count==2,"String-valued chapter times were accepted.");
        check(YouTubeChapters.ParsePage(page.Replace("Intro","Intro } \\\"quoted\\\""),id)[1].Title=="Intro } \"quoted\"","Quoted braces terminated the metadata object prematurely.");
        check(YouTubeChapters.ParsePage(page.Replace("playerOverlays","recommendations"),id).Count==0,"Recommended videos supplied the current video's chapters.");
        check(YouTubeChapters.ParsePage(page.Replace("\"timeRangeStartMillis\":133000","\"timeRangeStartMillis\":300000"),id).Select(c=>c.Start).SequenceEqual(new double[]{0,199,300}),"Chapter boundaries were not sorted.");
        Reject(()=>YouTubeChapters.ParsePage(page,"M7lc1UVf-VE"),check,"Another video's chapters were accepted.");
        Reject(()=>YouTubeChapters.ParsePage(page.Replace("var ytInitialData =","var unavailable ="),id),check,"Missing metadata was not reported.");
        Reject(()=>YouTubeChapters.ParsePage(new string('x',YouTubeChapters.MaxPageBytes+1),id),check,"Oversized watch page was accepted.");
    }
    private static void Reject(Action action,Action<bool,string> check,string message)
    {
        try{action();}catch(InvalidDataException){check(true,message);return;}check(false,message);
    }
}
