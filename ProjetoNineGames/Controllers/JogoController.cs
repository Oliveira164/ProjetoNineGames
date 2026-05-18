using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    public class JogoController : Controller
    {
        private readonly Database _db = new();

        // VITRINE — pública

        [HttpGet]
        public IActionResult Index(string? categoria, string? busca)
        {
            var lista = new List<Jogo>();

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_listar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_categoria", string.IsNullOrWhiteSpace(categoria) ? DBNull.Value : categoria);
            cmd.Parameters.AddWithValue("p_busca",     string.IsNullOrWhiteSpace(busca)     ? DBNull.Value : busca);

            using var rd = cmd.ExecuteReader();
            while (rd.Read()) lista.Add(MapearJogo(rd));
            rd.Close();

            ViewBag.Categorias    = ObterCategorias(conn);
            ViewBag.CategoriaAtual = categoria ?? "";
            ViewBag.Busca         = busca ?? "";

            if (TempData["ok"]   != null) ViewBag.Ok   = TempData["ok"];
            if (TempData["erro"] != null) ViewBag.Erro = TempData["erro"];

            return View(lista);
        }

        [HttpGet]
        public IActionResult Detalhe(int id)
        {
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_obter", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);

            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return NotFound();

            return View(MapearJogo(rd));
        }

        // GERENCIAR — Admin / Funcionario

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpGet]
        public IActionResult Gerenciar(string? busca)
        {
            var lista = new List<Jogo>();

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_listar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_categoria", DBNull.Value);
            cmd.Parameters.AddWithValue("p_busca",     string.IsNullOrWhiteSpace(busca) ? DBNull.Value : busca);

            using var rd = cmd.ExecuteReader();
            while (rd.Read()) lista.Add(MapearJogo(rd));
            rd.Close();

            ViewBag.Busca = busca ?? "";
            if (TempData["ok"]   != null) ViewBag.Ok   = TempData["ok"];
            if (TempData["erro"] != null) ViewBag.Erro = TempData["erro"];

            return View(lista);
        }

        // ── Criar ────────────────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpGet]
        public IActionResult Criar()
        {
            using var conn = _db.GetConnection();
            ViewBag.Categorias = CarregarSelectCategorias(conn);
            return View(new Jogo());
        }

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Criar(Jogo model, IFormFile? imagem)
        {
            if (string.IsNullOrWhiteSpace(model.Titulo))
            {
                using var conn2 = _db.GetConnection();
                ViewBag.Categorias = CarregarSelectCategorias(conn2);
                ModelState.AddModelError("", "Informe título e preço válido.");
                return View(model);
            }

            var imagemUrl = SalvarImagem(imagem);

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_criar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_titulo",     model.Titulo);
            cmd.Parameters.AddWithValue("p_descricao",  (object?)model.Descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_preco",      model.Preco);
            cmd.Parameters.AddWithValue("p_categoria",  (object?)model.Categoria ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_imagem_url", (object?)imagemUrl       ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Jogo cadastrado com sucesso!";
            return RedirectToAction(nameof(Gerenciar));
        }

        // ── Editar ───────────────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_obter", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return NotFound();
            var jogo = MapearJogo(rd);
            rd.Close();

            ViewBag.Categorias = CarregarSelectCategorias(conn);
            return View(jogo);
        }

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Editar(Jogo model, IFormFile? imagem)
        {
            if (string.IsNullOrWhiteSpace(model.Titulo))
            {
                using var conn2 = _db.GetConnection();
                ViewBag.Categorias = CarregarSelectCategorias(conn2);
                ModelState.AddModelError("", "Informe título e preço válido.");
                return View(model);
            }

            var imagemUrl = SalvarImagem(imagem) ?? model.ImagemUrl;

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_atualizar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id",         model.Id);
            cmd.Parameters.AddWithValue("p_titulo",     model.Titulo);
            cmd.Parameters.AddWithValue("p_descricao",  (object?)model.Descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_preco",      model.Preco);
            cmd.Parameters.AddWithValue("p_categoria",  (object?)model.Categoria ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_imagem_url", (object?)imagemUrl       ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Jogo atualizado!";
            return RedirectToAction(nameof(Gerenciar));
        }

        // ── Excluir ──────────────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Excluir(int id)
        {
            using var conn = _db.GetConnection();
            try
            {
                using var cmd = new MySqlCommand("sp_jogo_excluir", conn)
                                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("p_id", id);
                cmd.ExecuteNonQuery();
                TempData["ok"] = "Jogo excluído.";
            }
            catch (MySqlException ex)
            {
                TempData["erro"] = ex.Message;
            }

            return RedirectToAction(nameof(Gerenciar));
        }

        // Helpers

        private static Jogo MapearJogo(MySqlDataReader rd) => new()
        {
            Id        = rd.GetInt32("id"),
            Titulo    = rd.GetString("titulo"),
            Descricao = rd["descricao"]  as string,
            Preco     = rd.GetDecimal("preco"),
            Categoria = rd["categoria"]  as string,
            ImagemUrl = rd["imagem_url"] as string,
        };

        private static List<string> ObterCategorias(MySqlConnection conn)
        {
            var cats = new List<string>();
            using var cmd = new MySqlCommand("sp_jogo_categorias", conn)
                            { CommandType = CommandType.StoredProcedure };
            using var rd  = cmd.ExecuteReader();
            while (rd.Read()) cats.Add(rd.GetString("categoria"));
            return cats;
        }

        private static List<SelectListItem> CarregarSelectCategorias(MySqlConnection conn)
        {
            var lista = new List<SelectListItem>();
            using var cmd = new MySqlCommand("sp_categoria_listar", conn)
                            { CommandType = CommandType.StoredProcedure };
            using var rd  = cmd.ExecuteReader();
            while (rd.Read())
                lista.Add(new SelectListItem
                {
                    Value = rd.GetString("nome"),
                    Text  = rd.GetString("nome")
                });
            return lista;
        }

        private static string? SalvarImagem(IFormFile? imagem)
        {
            if (imagem == null || imagem.Length == 0) return null;
            var ext      = Path.GetExtension(imagem.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var dir      = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "jogos");
            Directory.CreateDirectory(dir);
            using var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            imagem.CopyTo(fs);
            return "jogos/" + fileName;
        }
    }
}
