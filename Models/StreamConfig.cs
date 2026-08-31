namespace SimpleSRT.App.Models
{
    public class StreamConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9998;
        
        // Define o modo de operação do protocolo SRT: "caller" (cliente) ou "listener" (servidor)
        public string Mode { get; set; } = "caller";
        
        public int LatencyMs { get; set; } = 120;
        public int NetworkCachingMs { get; set; } = 150;
        public string StreamId { get; set; } = string.Empty;

        public string ToSrtUrl()
        {
            var url = $"srt://{Host}:{Port}?mode={Mode.ToLower()}&latency={LatencyMs}";
            
            if (!string.IsNullOrWhiteSpace(StreamId))
            {
                url += $"&streamid={StreamId}";
            }

            return url;
        }
    }
}