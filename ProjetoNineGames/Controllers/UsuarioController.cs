using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OtpNet;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly Database _db = new();

        // ── Listagem (Admin) ─────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpGet]
        public IActionResult Index()
        {
            var lista = new List<Usuario>();

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_listar", conn)
                             { CommandType = CommandType.StoredProcedure };
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                lista.Add(new Usuario
                {
                    Id               = rd.GetInt32("id"),
                    Nome             = rd.GetString("nome"),
                    Email            = rd.GetString("email"),
                    Role             = rd.GetString("role"),
                    Ativo            = rd.GetInt32("ativo"),
                    TwoFactorEnabled = rd.GetBoolean("two_factor_enabled"),
                    CriadoEm         = rd.GetDateTime("criado_em"),
                });
            }

            return View(lista);
        }

        // ── Criar usuário (Admin) ────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpGet]
        public IActionResult Criar() => View(new Usuario());

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Criar(Usuario model, string senha)
        {
            if (string.IsNullOrWhiteSpace(model.Nome)  ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(senha))
            {
                ModelState.AddModelError("", "Preencha todos os campos obrigatórios.");
                return View(model);
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 12);

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_criar", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_nome",       model.Nome);
            cmd.Parameters.AddWithValue("p_email",      model.Email);
            cmd.Parameters.AddWithValue("p_senha_hash", hash);
            cmd.Parameters.AddWithValue("p_role",       model.Role ?? "Cliente");
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Usuário criado com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        // ── Ativar / Desativar (Admin) ───────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AlterarAtivo(int id, int ativo)
        {
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_alterar_ativo", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id",    id);
            cmd.Parameters.AddWithValue("p_ativo", ativo);
            cmd.ExecuteNonQuery();

            TempData["ok"] = ativo == 1 ? "Usuário ativado." : "Usuário desativado.";
            return RedirectToAction(nameof(Index));
        }

        // ── Perfil do usuário logado ─────────────────────────────────────────

        [SessionAuthorize]
        [HttpGet]
        public IActionResult Perfil()
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_obter_por_id", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return RedirectToAction("Login", "Auth");

            var u = new Usuario
            {
                Id               = rd.GetInt32("id"),
                Nome             = rd.GetString("nome"),
                Email            = rd.GetString("email"),
                Role             = rd.GetString("role"),
                TwoFactorEnabled = rd.GetBoolean("two_factor_enabled"),
            };

            return View(u);
        }

        // ── Ativar 2FA — gera secret e exibe QR ─────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Ativar2Fa()
        {
            var id    = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var email = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "";

            // Gera um secret Base32 aleatório de 20 bytes
            var secretBytes = KeyGeneration.GenerateRandomKey(20);
            var secret      = Base32Encoding.ToString(secretBytes);

            // URI padrão TOTP (compatível com Google Authenticator, etc.)
            var otpUri = $"otpauth://totp/NineGames:{Uri.EscapeDataString(email)}" +
                         $"?secret={secret}&issuer=NineGames&algorithm=SHA1&digits=6&period=30";

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_atualizar_2fa", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id",                id);
            cmd.Parameters.AddWithValue("p_enabled",           1);
            cmd.Parameters.AddWithValue("p_two_factor_secret", secret);
            cmd.ExecuteNonQuery();

            ViewBag.OtpUri = otpUri;
            ViewBag.Secret = secret;
            return View("Configurar2Fa");
        }

        // ── Desativar 2FA ────────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Desativar2Fa()
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_atualizar_2fa", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id",                id);
            cmd.Parameters.AddWithValue("p_enabled",           0);
            cmd.Parameters.AddWithValue("p_two_factor_secret", DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "2FA desativado.";
            return RedirectToAction(nameof(Perfil));
        }
    }
}
