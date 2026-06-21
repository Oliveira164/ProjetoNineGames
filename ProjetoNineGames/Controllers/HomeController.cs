using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Database _db = new();

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var lista = new List<Jogo>();

            try
            {
                using var conn = _db.GetConnection();
                using var cmd = new MySqlCommand("sp_jogo_listar", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("p_categoria", DBNull.Value);
                cmd.Parameters.AddWithValue("p_busca", DBNull.Value);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    lista.Add(MapearJogo(rd));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar jogos na Home Page");
            }

            return View(lista);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new Erro { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static Jogo MapearJogo(MySqlDataReader rd)
        {
            var jogo = new Jogo
            {
                Id = rd.GetInt32("id"),
                Titulo = rd.GetString("titulo"),
                Preco = rd.GetDecimal("preco")
            };

            if (HasColumn(rd, "categoria"))
                jogo.Categoria = rd["categoria"] as string;

            if (HasColumn(rd, "imagem_url"))
                jogo.ImagemUrl = rd["imagem_url"] as string;

            return jogo;
        }

        private static bool HasColumn(MySqlDataReader rd, string columnName)
        {
            for (int i = 0; i < rd.FieldCount; i++)
            {
                if (rd.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}