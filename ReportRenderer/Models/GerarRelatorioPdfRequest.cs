using System.Collections.Generic;

namespace ReportRenderer.Models
{
    public class GerarRelatorioPdfRequest
    {
        public GerarRelatorioPdfRequest()
        {
            Parametros = new List<string>();
        }

        public string Arquivo { get; set; }
        public string Relatorio { get; set; }
        public string Sql { get; set; }
        public string Conexao { get; set; }
        public List<string> Parametros { get; set; }
        public string Titulo { get; set; }
    }
}
