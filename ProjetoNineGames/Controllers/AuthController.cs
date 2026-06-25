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

        // ======================================================
        // ETAPA 1 — Login
        // ======================================================

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
            using var cmd = new MySqlCommand("sp_usuario_obter_por_email", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_email", email);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                ViewBag.Error = "Usuário não encontrado.";
                return View();
            }

            var id = rd.GetInt32("id");
            var nome = rd.GetString("nome");
            var role = rd.GetString("role");
            var ativo = rd.GetInt32("ativo");
            var senhaHash = rd["senha_hash"] as string ?? "";
            var twoFaEnabled = rd.GetBoolean("two_factor_enabled");
            var twoFaSecret = rd["two_factor_secret"] as string ?? "";

            // Lendo a foto do usuário
            var fotoUrl = rd.IsDBNull(rd.GetOrdinal("foto_url")) ? null : rd.GetString("foto_url");

            rd.Close();

            if (ativo == 0)
            {
                ViewBag.Error = "Usuário inativo.";
                return View();
            }

            bool senhaOk;
            try { senhaOk = BCrypt.Net.BCrypt.Verify(senha, senhaHash); }
            catch { senhaOk = false; }

            if (!senhaOk)
            {
                ViewBag.Error = "Senha ou e-mail inválidos.";
                return View();
            }

            if (twoFaEnabled && !string.IsNullOrWhiteSpace(twoFaSecret))
            {
                HttpContext.Session.SetInt32(SessionKeys.TwoFaPendingUserId, id);
                return RedirectToAction("Verificar2Fa", new { returnUrl });
            }

            // Repassando a fotoUrl para salvar na sessão
            SetarSessao(id, nome, email, role, fotoUrl);
            return RedirectLocal(returnUrl);
        }

        // ======================================================
        // ETAPA 2 — Verificação 2FA
        // ======================================================

        [HttpGet]
        public IActionResult Verificar2Fa(string? returnUrl = null)
        {
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

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_obter_por_id", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", pendingId.Value);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            var id = rd.GetInt32("id");
            var nome = rd.GetString("nome");
            var email = rd.GetString("email");
            var role = rd.GetString("role");
            var twoFaSecret = rd["two_factor_secret"] as string ?? "";

            // Lendo a foto do usuário também no 2FA
            var fotoUrl = rd.IsDBNull(rd.GetOrdinal("foto_url")) ? null : rd.GetString("foto_url");

            rd.Close();

            var secretBytes = Base32Encoding.ToBytes(twoFaSecret);
            var totp = new Totp(secretBytes);

            bool codigoValido = totp.VerifyTotp(
                codigo.Trim(),
                out _,
                new VerificationWindow(previous: 1, future: 1));

            if (!codigoValido)
            {
                ViewBag.Error = "Código inválido ou expirado. Tente novamente.";
                return View();
            }

            HttpContext.Session.Remove(SessionKeys.TwoFaPendingUserId);

            // Repassando a fotoUrl para salvar na sessão
            SetarSessao(id, nome, email, role, fotoUrl);
            return RedirectLocal(returnUrl);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AcessoNegado() => View();

        // ======================================================
        // RECUPERAÇÃO DE SENHA (SIMULAÇÃO TCC)
        // ======================================================

        [HttpGet]
        public IActionResult EsqueciSenha()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EsqueciSenha(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Informe o seu e-mail cadastrado.";
                return View();
            }

            string token = Guid.NewGuid().ToString();
            int minutosValidade = 15;

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_definir_token_recuperacao", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_email", email);
            cmd.Parameters.AddWithValue("p_token", token);
            cmd.Parameters.AddWithValue("p_minutos_validade", minutosValidade);
            cmd.ExecuteNonQuery();

            string? linkRedefinicao = Url.ActionLink("RedefinirSenha", "Auth", new { token = token });

            ViewBag.LinkTCC = linkRedefinicao;
            ViewBag.Success = "Simulação TCC: O e-mail de recuperação seria enviado. Clique no link abaixo para simular o acesso.";

            return View();
        }

        [HttpGet]
        public IActionResult RedefinirSenha(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Login");
            }

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_obter_por_token", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_token", token);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                ViewBag.Error = "Token inválido ou expirado.";
                return View("LinkInvalido");
            }

            var expiracao = rd.GetDateTime("token_expiracao");
            rd.Close();

            if (expiracao < DateTime.Now)
            {
                ViewBag.Error = "Este link de recuperação já expirou. Solicite um novo.";
                return View("LinkInvalido");
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RedefinirSenha(string token, string novaSenha, string confirmarSenha)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(novaSenha))
            {
                ViewBag.Error = "Preencha todos os campos.";
                ViewBag.Token = token;
                return View();
            }

            if (novaSenha != confirmarSenha)
            {
                ViewBag.Error = "As senhas não coincidem.";
                ViewBag.Token = token;
                return View();
            }

            using var conn = _db.GetConnection();
            using var cmdObter = new MySqlCommand("sp_usuario_obter_por_token", conn)
            { CommandType = CommandType.StoredProcedure };
            cmdObter.Parameters.AddWithValue("p_token", token);
            using var rd = cmdObter.ExecuteReader();

            if (!rd.Read())
            {
                ViewBag.Error = "Sessão de recuperação inválida.";
                return View("LinkInvalido");
            }

            var id = rd.GetInt32("id");
            var expiracao = rd.GetDateTime("token_expiracao");
            rd.Close();

            if (expiracao < DateTime.Now)
            {
                ViewBag.Error = "Este link já expirou.";
                return View("LinkInvalido");
            }

            string novaSenhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);

            using var cmdAtualizar = new MySqlCommand("sp_usuario_atualizar_senha", conn)
            { CommandType = CommandType.StoredProcedure };
            cmdAtualizar.Parameters.AddWithValue("p_id", id);
            cmdAtualizar.Parameters.AddWithValue("p_nova_senha_hash", novaSenhaHash);
            cmdAtualizar.ExecuteNonQuery();

            using var cmdLimparToken = new MySqlCommand("UPDATE usuarios SET token_recuperacao = NULL, token_expiracao = NULL WHERE id = @id", conn);
            cmdLimparToken.Parameters.AddWithValue("@id", id);
            cmdLimparToken.ExecuteNonQuery();

            TempData["Sucesso"] = "Sua senha foi redefinida com sucesso! Prossiga com o login.";
            return RedirectToAction("Login");
        }

        // ======================================================
        // HELPERS PRIVADOS
        // ======================================================

        // Parâmetro fotoUrl adicionado para gravar na sessão
        private void SetarSessao(int id, string nome, string email, string role, string? fotoUrl)
        {
            HttpContext.Session.SetInt32(SessionKeys.UserId, id);
            HttpContext.Session.SetString(SessionKeys.UserName, nome);
            HttpContext.Session.SetString(SessionKeys.UserEmail, email);
            HttpContext.Session.SetString(SessionKeys.UserRole, role);

            // Grava a URL da foto na Sessão se o usuário tiver uma cadastrada
            if (!string.IsNullOrEmpty(fotoUrl))
            {
                HttpContext.Session.SetString("UserPhotoUrl", fotoUrl);
            }
            else
            {
                HttpContext.Session.Remove("UserPhotoUrl");
            }
        }

        private IActionResult RedirectLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}