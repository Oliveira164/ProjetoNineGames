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

        // ETAPA 1 — Login

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

        // ETAPA 2 — Verificação do código TOTP (SteamGuard style)

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

        // Logout

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AcessoNegado() => View();

        // Helpers privados

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

        // ======================================================
        // RECUPERAÇÃO DE SENHA VIA TOKEN POR E-MAIL
        // ======================================================

        // ETAPA 1: Tela para digitar o e-mail
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

            // 1. Gera o token único
            string token = Guid.NewGuid().ToString();
            int minutosValidade = 15;

            // 2. Salva no banco (usando a Procedure que criamos)
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_definir_token_recuperacao", conn)
            { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_email", email);
            cmd.Parameters.AddWithValue("p_token", token);
            cmd.Parameters.AddWithValue("p_minutos_validade", minutosValidade);
            cmd.ExecuteNonQuery();

            // 3. Gera a URL apontando para a tela "RedefinirSenha"
            string? linkRedefinicao = Url.ActionLink("RedefinirSenha", "Auth", new { token = token });

            // 4. MODO TCC (Simulação): Passamos o link direto para a View
            ViewBag.LinkTCC = linkRedefinicao;
            ViewBag.Success = "Simulação TCC: O e-mail de recuperação seria enviado. Clique no botão abaixo para simular o usuário abrindo o e-mail.";

            return View();
        }

        // ETAPA 2: Tela que o usuário acessa ao clicar no link do e-mail
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
                return View("LinkInvalido"); // Uma view simples de erro
            }

            var expiracao = rd.GetDateTime("token_expiracao");
            rd.Close();

            // Valida se o token já expirou comparando com o horário atual do servidor
            if (expiracao < DateTime.Now)
            {
                ViewBag.Error = "Este link de recuperação já expirou. Solicite um novo.";
                return View("LinkInvalido");
            }

            // Passa o token para a View para que ele seja enviado de volta no POST
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
                return View();
            }

            var id = rd.GetInt32("id");
            var expiracao = rd.GetDateTime("token_expiracao");
            rd.Close();

            if (expiracao < DateTime.Now)
            {
                ViewBag.Error = "Este link já expirou.";
                return View();
            }

            // Criptografa a nova senha utilizando o BCrypt igual ao seu login
            string novaSenhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);

            // Atualiza a senha no banco usando a Procedure que criamos na resposta anterior
            using var cmdAtualizar = new MySqlCommand("sp_usuario_atualizar_senha", conn)
            { CommandType = CommandType.StoredProcedure };
            cmdAtualizar.Parameters.AddWithValue("p_id", id);
            cmdAtualizar.Parameters.AddWithValue("p_nova_senha_hash", novaSenhaHash);
            cmdAtualizar.ExecuteNonQuery();

            // LIMPEZA SEGURA: invalidar o token usado para que ele não possa ser reutilizado
            using var cmdLimparToken = new MySqlCommand("UPDATE usuarios SET token_recuperacao = NULL, token_expiracao = NULL WHERE id = @id", conn);
            cmdLimparToken.Parameters.AddWithValue("@id", id);
            cmdLimparToken.ExecuteNonQuery();

            TempData["Sucesso"] = "Sua senha foi redefinida com sucesso! Prossiga com o login.";
            return RedirectToAction("Login");
        }

        // Método fictício para exemplificar o envio de e-mail
        private void EnviarEmailRecuperacao(string emailDestinatario, string? link)
        {
            // Exemplo de corpo do e-mail:
            string assunto = "Recuperação de Senha - NineGames";
            string corpo = $"Olá! Você solicitou a redefinição de sua senha. Clique no link a seguir para cadastrar uma nova senha: {link}. Este link é válido por 15 minutos.";

            // Logica de envio real iria aqui...
        }
    }
}
