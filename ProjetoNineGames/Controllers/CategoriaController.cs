using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
    public class CategoriaController : Controller
    {
        private readonly Database _db = new();

        [HttpGet]
        public IActionResult Index()
        {
            var lista = new List<Categoria>();

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_categoria_listar", conn)
                             { CommandType = CommandType.StoredProcedure };
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                lista.Add(new Categoria
                {
                    Id        = rd.GetInt32("id"),
                    Nome      = rd.GetString("nome"),
                    Descricao = rd["descricao"] as string,
                    CriadoEm  = rd.GetDateTime("criado_em"),
                });
            }

            return View(lista);
        }

        [HttpGet]
        public IActionResult Criar() => View(new Categoria());

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Criar(Categoria model)
        {
            if (string.IsNullOrWhiteSpace(model.Nome))
            {
                ModelState.AddModelError("", "Nome é obrigatório.");
                return View(model);
            }

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_categoria_criar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_nome",      model.Nome);
            cmd.Parameters.AddWithValue("p_descricao", (object?)model.Descricao ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Categoria criada!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_categoria_obter", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return NotFound();

            return View(new Categoria
            {
                Id        = rd.GetInt32("id"),
                Nome      = rd.GetString("nome"),
                Descricao = rd["descricao"] as string,
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Editar(Categoria model)
        {
            if (string.IsNullOrWhiteSpace(model.Nome))
            {
                ModelState.AddModelError("", "Nome é obrigatório.");
                return View(model);
            }

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_categoria_atualizar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id",        model.Id);
            cmd.Parameters.AddWithValue("p_nome",      model.Nome);
            cmd.Parameters.AddWithValue("p_descricao", (object?)model.Descricao ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Categoria atualizada!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Excluir(int id)
        {
            using var conn = _db.GetConnection();
            try
            {
                using var cmd = new MySqlCommand("sp_categoria_excluir", conn)
                                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("p_id", id);
                cmd.ExecuteNonQuery();
                TempData["ok"] = "Categoria excluída.";
            }
            catch (MySqlException ex)
            {
                TempData["erro"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
