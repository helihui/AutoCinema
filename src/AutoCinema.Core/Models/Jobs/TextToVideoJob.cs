using AutoCinema.Pro.Models;

namespace AutoCinema.Pro.Models.Jobs;

public class TextToVideoJob : JobItem
{
    public TextToVideoJob()
    {
        Type = JobType.TextOnVideo;
    }

    public required VideoProject ProjectData { get; set; }
}
