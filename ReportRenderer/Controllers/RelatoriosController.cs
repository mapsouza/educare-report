using C1.Win.C1Document.Export;
using C1.Win.FlexReport;
using ReportRenderer.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace ReportRenderer.Controllers
{
    [RoutePrefix("api/relatorios")]
    public class RelatoriosController : ApiController
    {
        private const string ExtensaoRelatorio = ".flxr";
        private const string PastaRelatorios = "~/App_Data/RelatoriosFlexReport";
        private static readonly object RenderizacaoLock = new object();
        private static readonly Regex NomeArquivoValido = new Regex(@"^[a-zA-Z0-9_. -]+\.flxr$", RegexOptions.Compiled);

        [HttpPost]
        [Route("upload-flxr")]
        public async Task<IHttpActionResult> UploadFlxr()
        {
            var erroAutorizacao = ValidarApiKey();
            if (erroAutorizacao != null)
                return erroAutorizacao;

            if (!Request.Content.IsMimeMultipartContent())
                return BadRequest("Envie o arquivo como multipart/form-data.");

            var pasta = ObterPastaRelatorios();
            Directory.CreateDirectory(pasta);

            var provider = new MultipartFormDataStreamProvider(pasta);
            await Request.Content.ReadAsMultipartAsync(provider);

            var arquivo = provider.FileData.FirstOrDefault();
            if (arquivo == null)
                return BadRequest("Arquivo FLXR não informado.");

            var nomeOriginal = arquivo.Headers.ContentDisposition.FileName;
            var nomeArquivo = Path.GetFileName(string.IsNullOrWhiteSpace(nomeOriginal) ? null : nomeOriginal.Trim('"'));
            if (!NomeArquivoEhValido(nomeArquivo))
            {
                File.Delete(arquivo.LocalFileName);
                return BadRequest("Nome de arquivo FLXR inválido.");
            }

            var destino = Path.Combine(pasta, nomeArquivo);
            if (File.Exists(destino))
                File.Delete(destino);

            File.Move(arquivo.LocalFileName, destino);
            return Ok(new { arquivo = nomeArquivo });
        }

        [HttpPost]
        [Route("gerar-pdf")]
        public HttpResponseMessage GerarPdf(GerarRelatorioPdfRequest request)
        {
            var erroAutorizacao = ValidarApiKeyResponse();
            if (erroAutorizacao != null)
                return erroAutorizacao;

            var erro = ValidarRequest(request);
            if (!string.IsNullOrWhiteSpace(erro))
                return CriarErro(HttpStatusCode.BadRequest, erro);

            var caminhoArquivo = ObterCaminhoRelatorio(request.Arquivo);
            if (!File.Exists(caminhoArquivo))
                return CriarErro(HttpStatusCode.NotFound, "Arquivo FLXR não encontrado.");

            try
            {
                var pdf = ExecutarEmSta(() =>
                {
                    lock (RenderizacaoLock)
                    {
                        return GerarPdfInterno(caminhoArquivo, request);
                    }
                });

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pdf)
                };

                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = Path.GetFileNameWithoutExtension(request.Arquivo) + ".pdf"
                };

                return response;
            }
            catch (Exception ex)
            {
                return CriarErro(HttpStatusCode.InternalServerError, "Erro ao gerar relatório PDF.", ex.ToString());
            }
        }

        private static byte[] GerarPdfInterno(string caminhoArquivo, GerarRelatorioPdfRequest request)
        {
            using (var relatorio = new C1FlexReport())
            {
                relatorio.Load(caminhoArquivo, request.Relatorio);
                AjustarConexoes(relatorio, request.Conexao);

                if (relatorio.DataSource == null)
                    throw new InvalidOperationException("O relatório carregado não possui DataSource principal.");

                relatorio.DataSource.RecordSource = request.Sql;
                AjustarParametros(relatorio, request);
                relatorio.Render();

                using (var stream = new MemoryStream())
                using (var filtro = new PdfFilter { ShowOptions = false, Preview = false, Stream = stream })
                {
                    relatorio.RenderToFilter(filtro);
                    return stream.ToArray();
                }
            }
        }

        private static void AjustarConexoes(C1FlexReport relatorio, string conexao)
        {
            if (relatorio.DataSources != null)
            {
                foreach (DataSource dataSource in relatorio.DataSources)
                {
                    if (dataSource == null)
                        continue;

                    dataSource.DataProvider = DataProvider.OLEDB;
                    dataSource.ConnectionString = conexao;
                }
            }

            if (relatorio.Fields == null)
                return;

            foreach (FieldBase campo in relatorio.Fields)
            {
                var subreportField = campo as SubreportField;
                if (subreportField != null && subreportField.Subreport != null)
                {
                    AjustarConexoes(subreportField.Subreport, conexao);
                    continue;
                }

                var field = campo as Field;
                if (field != null && field.Subreport != null)
                    AjustarConexoes(field.Subreport, conexao);
            }
        }

        private static void AjustarParametros(C1FlexReport relatorio, GerarRelatorioPdfRequest request)
        {
            if (relatorio.Parameters == null)
                return;

            ReportParameter parametroLista = null;

            foreach (ReportParameter parametro in relatorio.Parameters)
            {
                if (parametro == null)
                    continue;

                if (string.Equals(parametro.Name, "TITULO", StringComparison.OrdinalIgnoreCase))
                {
                    parametro.Value = request.Titulo;
                }
                else if (parametroLista == null)
                {
                    parametroLista = parametro;
                }
            }

            if (request.Parametros != null && request.Parametros.Count > 0)
            {
                parametroLista = parametroLista ?? (relatorio.Parameters.Count > 0 ? relatorio.Parameters[0] : null);
                if (parametroLista != null)
                    parametroLista.Value = request.Parametros.ToArray();
            }
        }

        private static T ExecutarEmSta<T>(Func<T> func)
        {
            T resultado = default(T);
            Exception erro = null;

            var thread = new Thread(() =>
            {
                try
                {
                    resultado = func();
                }
                catch (Exception ex)
                {
                    erro = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (erro != null)
                throw erro;

            return resultado;
        }

        private IHttpActionResult ValidarApiKey()
        {
            var response = ValidarApiKeyResponse();
            return response == null ? null : ResponseMessage(response);
        }

        private HttpResponseMessage ValidarApiKeyResponse()
        {
            var apiKey = ConfigurationManager.AppSettings["ReportApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            IEnumerable<string> valores;
            if (!Request.Headers.TryGetValues("X-Report-Api-Key", out valores) || valores.FirstOrDefault() != apiKey)
                return CriarErro(HttpStatusCode.Unauthorized, "Chave de acesso inválida.");

            return null;
        }

        private static HttpResponseMessage CriarErro(HttpStatusCode statusCode, string mensagem, string detalhe = null)
        {
            return RequestMessageHelper.CreateJsonError(statusCode, mensagem, detalhe);
        }

        private static string ObterPastaRelatorios()
        {
            return HttpContext.Current.Server.MapPath(PastaRelatorios);
        }

        private static string ObterCaminhoRelatorio(string arquivo)
        {
            return Path.Combine(ObterPastaRelatorios(), Path.GetFileName(arquivo));
        }

        private static string ValidarRequest(GerarRelatorioPdfRequest request)
        {
            if (request == null)
                return "Dados do relatório não informados.";

            if (!NomeArquivoEhValido(request.Arquivo))
                return "Arquivo FLXR inválido.";

            if (string.IsNullOrWhiteSpace(request.Relatorio))
                return "Nome do relatório não informado.";

            if (string.IsNullOrWhiteSpace(request.Sql))
                return "SQL do relatório não informado.";

            if (string.IsNullOrWhiteSpace(request.Conexao))
                return "String de conexão não informada.";

            return null;
        }

        private static bool NomeArquivoEhValido(string arquivo)
        {
            if (string.IsNullOrWhiteSpace(arquivo))
                return false;

            var nomeArquivo = Path.GetFileName(arquivo);
            return string.Equals(nomeArquivo, arquivo, StringComparison.Ordinal)
                   && nomeArquivo.EndsWith(ExtensaoRelatorio, StringComparison.OrdinalIgnoreCase)
                   && NomeArquivoValido.IsMatch(nomeArquivo);
        }
    }

    internal static class RequestMessageHelper
    {
        public static HttpResponseMessage CreateJsonError(HttpStatusCode statusCode, string mensagem, string detalhe)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ObjectContent<object>(new { erro = mensagem, detalhe }, GlobalConfiguration.Configuration.Formatters.JsonFormatter)
            };

            return response;
        }
    }
}
