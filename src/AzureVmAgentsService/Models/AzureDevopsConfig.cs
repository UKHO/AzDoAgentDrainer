using System;

namespace AzureVmAgentsService.Models
{
    public class AzureDevopsConfig
    {
        public Uri Uri { get; set; }
        public string Pat { get; set; }
        public string ComputerName { get; set; }        
    }
}


