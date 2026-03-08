using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoSynchronizer.CLI
{
    public class OfflineTokenDto
    {
        public string CanvasToken { get; set; }
        public string GraphToken { get; set; }
        public DidaCredential DidaCredential { get; set; }
        public KanboardCredential KanboardCredential { get; set; }
    }

    public class DidaCredential
    {
        public string phone { get; set; }
        public string password { get; set; }
    }

    public class KanboardCredential
    {
        public string Url { get; set; }
        public string ApiToken { get; set; }
    }
}
