using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OtpNet;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    public class AuthController : Controller
    {
        private readonly Database _db = new();

        // ────────────────────────────────────────────────────────────────────
        // ETAPA 1 — Login (email + senha)
        // ────────────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Login(string email, string senha, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                ViewBag.Error = "Informe e-mail e senha.";
                return View();
            }

            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_obter_por_email", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_email", email);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                ViewBag.Error = "Usuário não encontrado.";
                return View();
            }

            var id               = rd.GetInt32("id");
            var nome             = rd.GetString("nome");
            var role             = rd.GetString("role");
            var ativo            = rd.GetInt32("ativo");
            var senhaHash        = rd["senha_hash"] as string ?? "";
            var twoFaEnabled     = rd.GetBoolean("two_factor_enabled");
            var twoFaSecret      = rd["two_factor_secret"] as string ?? "";
            rd.Close();

            if (ativo == 0)
            {
                ViewBag.Error = "Usuário inativo.";
                return View();
            }

            bool senhaOk;
            try   { senhaOk = BCrypt.Net.BCrypt.Verify(senha, senhaHash); }
            catch { senhaOk = false; }

            if (!senhaOk)
            {
                ViewBag.Error = "Senha inválida.";
                return View();
            }

            // ── Senha OK ────────────────────────────────────────────────────
            if (twoFaEnabled && !string.IsNullOrWhiteSpace(twoFaSecret))
            {
                // Guarda apenas o ID pendente e redireciona para a etapa 2
                HttpContext.Session.SetInt32(SessionKeys.TwoFaPendingUserId, id);
                return RedirectToAction("Verificar2Fa", new { returnUrl });
            }

            // Sem 2FA → sessão completa direto
            SetarSessao(id, nome, email, role);
            return RedirectLocal(returnUrl);
        }

        // ────────────────────────────────────────────────────────────────────
        // ETAPA 2 — Verificação do código TOTP (SteamGuard style)
        // ────────────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Verificar2Fa(string? returnUrl = null)
        {
            // Impede acesso direto sem ter passado pela etapa 1
            if (HttpContext.Session.GetInt32(SessionKeys.TwoFaPendingUserId) == null)
                return RedirectToAction("Login");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Verificar2Fa(string codigo, string? returnUrl = null)
        {
            var pendingId = HttpContext.Session.GetInt32(SessionKeys.TwoFaPendingUserId);
            if (pendingId == null)
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(codigo))
            {
                ViewBag.Error = "Informe o código do aplicativo.";
                return View();
            }

            // Busca o secret do usuário pendente
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_usuario_obter_por_id", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", pendingId.Value);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            var id          = rd.GetInt32("id");
            var nome        = rd.GetString("nome");
            var email       = rd.GetString("email");
            var role        = rd.GetString("role");
            var twoFaSecret = rd["two_factor_secret"] as string ?? "";
            rd.Close();

            // ── Valida o código TOTP ────────────────────────────────────────
            // OtpNet espera o secret em Base32
            var secretBytes = Base32Encoding.ToBytes(twoFaSecret);
            var totp        = new Totp(secretBytes);

            // VerifyTotp aceita janela de ±1 intervalo (30 s) para compensar
            // pequenas diferenças de relógio entre celular e servidor.
            bool codigoValido = totp.VerifyTotp(
                codigo.Trim(),
                out _,
                new VerificationWindow(previous: 1, future: 1));

            if (!codigoValido)
            {
                ViewBag.Error = "Código inválido ou expirado. Tente novamente.";
                return View();
            }

            // ── Código OK → limpa pendência e cria sessão completa ──────────
            HttpContext.Session.Remove(SessionKeys.TwoFaPendingUserId);
            SetarSessao(id, nome, email, role);
            return RedirectLocal(returnUrl);
        }

        // ────────────────────────────────────────────────────────────────────
        // Logout
        // ────────────────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AcessoNegado() => View();

        // ────────────────────────────────────────────────────────────────────
        // Helpers privados
        // ────────────────────────────────────────────────────────────────────

        private void SetarSessao(int id, string nome, string email, string role)
        {
            HttpContext.Session.SetInt32(SessionKeys.UserId,    id);
            HttpContext.Session.SetString(SessionKeys.UserName,  nome);
            HttpContext.Session.SetString(SessionKeys.UserEmail, email);
            HttpContext.Session.SetString(SessionKeys.UserRole,  role);
        }

        private IActionResult RedirectLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
