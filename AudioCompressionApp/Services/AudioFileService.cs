using System.IO;

namespace AudioCompressionApp.Services;

public class AudioFileService {
    public bool IsSupportedAudioFile(string path) {
        string extension = Path.GetExtension(path).ToLower();
        return extension is ".wav" or ".ogg" or ".mp3";
    }   
}