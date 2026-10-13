using VICE.YouTube;

var checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
YouTubeTests.Run(Check);YouTubeChapterTests.Run(Check);
if(args.Contains("--youtube-chapters-live"))
{
    var live=await YouTubeChapters.Fetch("WJNppjUrmxM",default);
    Check(live.Chapters.Count>=3&&live.Chapters.Any(c=>c.Start==133&&c.Title=="Intro"),"Live chapters unavailable: "+live.Status);
    Console.WriteLine($"Live WJNppjUrmxM: {live.Chapters.Count} chapters.");
}
Console.WriteLine($"PASS: {checks} YouTube add-on playback, frame, chapter and protocol checks.");
