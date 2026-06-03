using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    [SessionAuthorize]
    public class BibliotecaController : Controller
    {
        private readonly Database _db = new();

        // ── Biblioteca com filtro ────────────────────────────────────────────

        [HttpGet]
        public IActionResult MinhaBiblioteca(string? categoria, string? busca)
        {
            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var jogos     = new List<Jogo>();

            using var conn = _db.GetConnection();

            // 1) Busca jogos com filtros aplicados
            using (var cmd = new MySqlCommand("sp_biblioteca_listar_filtro", conn)
                             { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("p_id_usuario", idUsuario);
                cmd.Parameters.AddWithValue("p_categoria",
                    string.IsNullOrWhiteSpace(categoria) ? DBNull.Value : categoria);
                cmd.Parameters.AddWithValue("p_busca",
                    string.IsNullOrWhiteSpace(busca) ? DBNull.Value : busca);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    jogos.Add(new Jogo
                    {
                        Id        = rd.GetInt32("id"),
                        Titulo    = rd.GetString("titulo"),
                        Descricao = rd["descricao"]  as string,
                        Categoria = rd["categoria"]  as string,
                        ImagemUrl = rd["imagem_url"] as string,
                        Preco     = rd.GetDecimal("preco"),
                    });
                }
            }

            // 2) Busca categorias disponíveis na biblioteca do usuário
            var categorias = new List<string>();
            using (var cmd = new MySqlCommand("sp_biblioteca_categorias", conn)
                             { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("p_id_usuario", idUsuario);
                using var rd = cmd.ExecuteReader();
                while (rd.Read()) categorias.Add(rd.GetString("categoria"));
            }

            ViewBag.Categorias     = categorias;
            ViewBag.CategoriaAtual = categoria ?? "";
            ViewBag.Busca          = busca ?? "";

            if (TempData["ok"] != null) ViewBag.Ok = TempData["ok"];
            return View(jogos);
        }

        // ── Lista de Desejos ─────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Wishlist()
        {
            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var jogos     = new List<Jogo>();

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_wishlist_listar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id_usuario", idUsuario);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                jogos.Add(new Jogo
                {
                    Id        = rd.GetInt32("id"),
                    Titulo    = rd.GetString("titulo"),
                    Categoria = rd["categoria"] as string,
                    ImagemUrl = rd["imagem_url"] as string,
                    Preco     = rd.GetDecimal("preco"),
                });
            }

            return View(jogos);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AdicionarWishlist(int jogoId)
        {
            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_wishlist_adicionar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id_usuario", idUsuario);
            cmd.Parameters.AddWithValue("p_id_jogo",    jogoId);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Adicionado à lista de desejos!";
            return RedirectToAction("Index", "Jogo");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RemoverWishlist(int jogoId)
        {
            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_wishlist_remover", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id_usuario", idUsuario);
            cmd.Parameters.AddWithValue("p_id_jogo",    jogoId);
            cmd.ExecuteNonQuery();

            return RedirectToAction(nameof(Wishlist));
        }
    }
}
