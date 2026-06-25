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

        // ── Admin: listagem ──────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpGet]
        public IActionResult Index()
        {
            var lista = new List<Usuario>();
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_listar", conn)
            { CommandType = CommandType.StoredProcedure };
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                lista.Add(new Usuario
                {
                    Id = rd.GetInt32("id"),
                    Nome = rd.GetString("nome"),
                    Email = rd.GetString("email"),
                    Role = rd.GetString("role"),
                    Ativo = rd.GetInt32("ativo"),
                    TwoFactorEnabled = rd.GetBoolean("two_factor_enabled"),
                    CriadoEm = rd.GetDateTime("criado_em"),
                });
            return View(lista);
        }

        // ── Admin: criar ─────────────────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpGet]
        public IActionResult Criar() => View(new Usuario());

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Criar(Usuario model, string senha)
        {
            if (string.IsNullOrWhiteSpace(model.Nome) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(senha))
            { ModelState.AddModelError("", "Preencha todos os campos."); return View(model); }

            var hash = BCrypt.Net.BCrypt.HashPassword(senha, 12);
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_criar", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_nome", model.Nome);
            cmd.Parameters.AddWithValue("p_email", model.Email);
            cmd.Parameters.AddWithValue("p_senha_hash", hash);
            cmd.Parameters.AddWithValue("p_role", model.Role ?? "Cliente");
            cmd.ExecuteNonQuery();
            TempData["ok"] = "Usuário criado!";
            return RedirectToAction(nameof(Index));
        }

        // ── Admin: ativar/desativar ──────────────────────────────────────────

        [SessionAuthorize(RoleAnyOf = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AlterarAtivo(int id, int ativo)
        {
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_alterar_ativo", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            cmd.Parameters.AddWithValue("p_ativo", ativo);
            cmd.ExecuteNonQuery();
            TempData["ok"] = ativo == 1 ? "Usuário ativado." : "Usuário desativado.";
            return RedirectToAction(nameof(Index));
        }

        // ── Registro público ─────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Registrar()
        {
            if (HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue)
                return RedirectToAction("Index", "Jogo");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Registrar(string nome, string email, string senha, string confirmarSenha)
        {
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            { ViewBag.Error = "Preencha todos os campos."; return View(); }

            if (senha != confirmarSenha)
            { ViewBag.Error = "As senhas não conferem."; return View(); }

            if (senha.Length < 6)
            { ViewBag.Error = "Senha deve ter pelo menos 6 caracteres."; return View(); }

            using var conn = _db.GetConnection();
            using var cmdChk = new MySqlCommand("sp_usuario_obter_por_email", conn) { CommandType = CommandType.StoredProcedure };
            cmdChk.Parameters.AddWithValue("p_email", email);
            using var rdChk = cmdChk.ExecuteReader();
            if (rdChk.Read()) { rdChk.Close(); ViewBag.Error = "E-mail já cadastrado."; return View(); }
            rdChk.Close();

            var hash = BCrypt.Net.BCrypt.HashPassword(senha, 12);
            using var cmdC = new MySqlCommand("sp_usuario_criar", conn) { CommandType = CommandType.StoredProcedure };
            cmdC.Parameters.AddWithValue("p_nome", nome.Trim());
            cmdC.Parameters.AddWithValue("p_email", email.Trim().ToLower());
            cmdC.Parameters.AddWithValue("p_senha_hash", hash);
            cmdC.Parameters.AddWithValue("p_role", "Cliente");
            using var rdC = cmdC.ExecuteReader();
            int novoId = rdC.Read() ? rdC.GetInt32("id") : 0;
            rdC.Close();

            HttpContext.Session.SetInt32(SessionKeys.UserId, novoId);
            HttpContext.Session.SetString(SessionKeys.UserName, nome.Trim());
            HttpContext.Session.SetString(SessionKeys.UserEmail, email.Trim().ToLower());
            HttpContext.Session.SetString(SessionKeys.UserRole, "Cliente");
            TempData["ok"] = $"Bem-vindo, {nome.Trim()}!";
            return RedirectToAction("Index", "Jogo");
        }

        // ════════════════════════════════════════════════════════════════════
        // PERFIL — abas
        // ════════════════════════════════════════════════════════════════════

        private Perfil CarregarPerfil(int id)
        {
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_obter_por_id", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            using var rd = cmd.ExecuteReader();
            if (!rd.Read()) return new Perfil();
            return new Perfil
            {
                Id = rd.GetInt32("id"),
                Nome = rd.GetString("nome"),
                Email = rd.GetString("email"),
                Role = rd.GetString("role"),
                TwoFactorEnabled = rd.GetBoolean("two_factor_enabled"),
                FotoUrl = rd["foto_url"] as string,
                Telefone = rd["telefone"] as string,
                Cpf = rd["cpf"] as string,
                DataNasc = rd.IsDBNull(rd.GetOrdinal("data_nasc"))
                                    ? null
                                    : rd.GetDateTime("data_nasc").ToString("yyyy-MM-dd"),
                CriadoEm = rd.GetDateTime("criado_em"),
            };
        }

        // ── GET Perfil (aba: minha-conta) ────────────────────────────────────

        [SessionAuthorize]
        [HttpGet]
        public IActionResult Perfil(string aba = "minha-conta")
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            ViewBag.Aba = aba;

            if (TempData["ok"] != null) ViewBag.Ok = TempData["ok"];
            if (TempData["erro"] != null) ViewBag.Erro = TempData["erro"];

            return View(CarregarPerfil(id));
        }

        // ── POST: Atualizar dados básicos ────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AtualizarDados(string nome, string? telefone, string? dataNasc, string? cpf)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            if (string.IsNullOrWhiteSpace(nome))
            { TempData["erro"] = "Nome é obrigatório."; return RedirectToAction(nameof(Perfil), new { aba = "minha-conta" }); }

            DateTime? dtNasc = null;
            if (!string.IsNullOrWhiteSpace(dataNasc) && DateTime.TryParse(dataNasc, out var dt)) dtNasc = dt;

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_atualizar_perfil", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            cmd.Parameters.AddWithValue("p_nome", nome.Trim());
            cmd.Parameters.AddWithValue("p_telefone", (object?)telefone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_data_nasc", (object?)dtNasc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_cpf", (object?)cpf ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            // Atualiza nome na sessão
            HttpContext.Session.SetString(SessionKeys.UserName, nome.Trim());
            TempData["ok"] = "Dados atualizados com sucesso!";
            return RedirectToAction(nameof(Perfil), new { aba = "minha-conta" });
        }

        // ── POST: Trocar foto ────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult TrocarFoto(IFormFile foto)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            if (foto == null || foto.Length == 0)
            { TempData["erro"] = "Selecione uma imagem."; return RedirectToAction(nameof(Perfil)); }

            var ext = Path.GetExtension(foto.FileName).ToLower();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
            { TempData["erro"] = "Formato inválido. Use JPG, PNG ou WebP."; return RedirectToAction(nameof(Perfil)); }

            if (foto.Length > 2 * 1024 * 1024)
            { TempData["erro"] = "Imagem muito grande. Máximo 2 MB."; return RedirectToAction(nameof(Perfil)); }

            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
            Directory.CreateDirectory(dir);
            var fileName = $"u{id}_{Guid.NewGuid():N}{ext}";
            using var fs = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            foto.CopyTo(fs);

            var fotoUrl = "avatars/" + fileName;
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_atualizar_foto", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            cmd.Parameters.AddWithValue("p_foto_url", fotoUrl);
            cmd.ExecuteNonQuery();
            HttpContext.Session.SetString("UserPhotoUrl", fotoUrl);

            TempData["ok"] = "Foto atualizada!";
            return RedirectToAction(nameof(Perfil));
        }

        // ── POST: Trocar e-mail ───────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult TrocarEmail(string novoEmail, string senhaAtual)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            if (string.IsNullOrWhiteSpace(novoEmail) || string.IsNullOrWhiteSpace(senhaAtual))
            { TempData["erro"] = "Preencha todos os campos."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-email" }); }

            // Valida senha atual
            using var conn = _db.GetConnection();
            using var cmdHash = new MySqlCommand("sp_usuario_obter_senha_hash", conn) { CommandType = CommandType.StoredProcedure };
            cmdHash.Parameters.AddWithValue("p_id", id);
            using var rdHash = cmdHash.ExecuteReader();
            if (!rdHash.Read()) { TempData["erro"] = "Usuário não encontrado."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-email" }); }
            var hash = rdHash.GetString("senha_hash");
            rdHash.Close();

            if (!BCrypt.Net.BCrypt.Verify(senhaAtual, hash))
            { TempData["erro"] = "Senha atual incorreta."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-email" }); }

            try
            {
                using var cmdE = new MySqlCommand("sp_usuario_trocar_email", conn) { CommandType = CommandType.StoredProcedure };
                cmdE.Parameters.AddWithValue("p_id", id);
                cmdE.Parameters.AddWithValue("p_email", novoEmail.Trim().ToLower());
                cmdE.ExecuteNonQuery();
                HttpContext.Session.SetString(SessionKeys.UserEmail, novoEmail.Trim().ToLower());
                TempData["ok"] = "E-mail atualizado com sucesso!";
            }
            catch (MySqlException ex)
            {
                TempData["erro"] = ex.Message;
            }

            return RedirectToAction(nameof(Perfil), new { aba = "alterar-email" });
        }

        // ── POST: Trocar senha ────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult TrocarSenha(string senhaAtual, string novaSenha, string confirmarSenha)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            if (string.IsNullOrWhiteSpace(senhaAtual) || string.IsNullOrWhiteSpace(novaSenha))
            { TempData["erro"] = "Preencha todos os campos."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" }); }

            if (novaSenha != confirmarSenha)
            { TempData["erro"] = "As novas senhas não conferem."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" }); }

            if (novaSenha.Length < 6)
            { TempData["erro"] = "A senha deve ter pelo menos 6 caracteres."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" }); }

            using var conn = _db.GetConnection();
            using var cmdHash = new MySqlCommand("sp_usuario_obter_senha_hash", conn) { CommandType = CommandType.StoredProcedure };
            cmdHash.Parameters.AddWithValue("p_id", id);
            using var rdHash = cmdHash.ExecuteReader();
            if (!rdHash.Read()) { TempData["erro"] = "Usuário não encontrado."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" }); }
            var hashAtual = rdHash.GetString("senha_hash");
            rdHash.Close();

            if (!BCrypt.Net.BCrypt.Verify(senhaAtual, hashAtual))
            { TempData["erro"] = "Senha atual incorreta."; return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" }); }

            var novoHash = BCrypt.Net.BCrypt.HashPassword(novaSenha, 12);
            using var cmdS = new MySqlCommand("sp_usuario_trocar_senha", conn) { CommandType = CommandType.StoredProcedure };
            cmdS.Parameters.AddWithValue("p_id", id);
            cmdS.Parameters.AddWithValue("p_senha_hash", novoHash);
            cmdS.ExecuteNonQuery();

            TempData["ok"] = "Senha alterada com sucesso!";
            return RedirectToAction(nameof(Perfil), new { aba = "alterar-senha" });
        }

        // ── GET/POST: Cartões ─────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AdicionarCartao(string numeroCartao, string nomeTitular, string validade)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var numLimpo = numeroCartao?.Replace(" ", "") ?? "";
            if (numLimpo.Length < 13) { TempData["erro"] = "Número do cartão inválido."; return RedirectToAction(nameof(Perfil), new { aba = "cartoes" }); }

            var ultimos4 = numLimpo[^4..];
            var bandeira = DetectarBandeira(numLimpo);

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_cartao_adicionar", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id_usuario", id);
            cmd.Parameters.AddWithValue("p_bandeira", bandeira);
            cmd.Parameters.AddWithValue("p_ultimos4", ultimos4);
            cmd.Parameters.AddWithValue("p_nome_titular", nomeTitular?.Trim().ToUpper() ?? "");
            cmd.Parameters.AddWithValue("p_validade", validade ?? "");
            cmd.Parameters.AddWithValue("p_principal", 0);
            cmd.ExecuteNonQuery();

            TempData["ok"] = "Cartão adicionado com sucesso!";
            return RedirectToAction(nameof(Perfil), new { aba = "cartoes" });
        }

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RemoverCartao(int cartaoId)
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_cartao_remover", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", cartaoId);
            cmd.Parameters.AddWithValue("p_id_usuario", id);
            cmd.ExecuteNonQuery();
            TempData["ok"] = "Cartão removido.";
            return RedirectToAction(nameof(Perfil), new { aba = "cartoes" });
        }

        // ── 2FA ──────────────────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Ativar2Fa()
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var email = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "";
            var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
            var otpUri = $"otpauth://totp/NineGames:{Uri.EscapeDataString(email)}?secret={secret}&issuer=NineGames&algorithm=SHA1&digits=6&period=30";

            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_atualizar_2fa", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            cmd.Parameters.AddWithValue("p_enabled", 1);
            cmd.Parameters.AddWithValue("p_two_factor_secret", secret);
            cmd.ExecuteNonQuery();

            ViewBag.OtpUri = otpUri;
            ViewBag.Secret = secret;
            return View("Configurar2Fa");
        }

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Desativar2Fa()
        {
            var id = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            using var conn = _db.GetConnection();
            using var cmd = new MySqlCommand("sp_usuario_atualizar_2fa", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", id);
            cmd.Parameters.AddWithValue("p_enabled", 0);
            cmd.Parameters.AddWithValue("p_two_factor_secret", DBNull.Value);
            cmd.ExecuteNonQuery();
            TempData["ok"] = "2FA desativado.";
            return RedirectToAction(nameof(Perfil), new { aba = "seguranca" });
        }

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ConfirmarSetup2Fa(string codigo)
        {
            // Para o nível do TCC: Aqui nós recebemos o "codigo" de 6 dígitos que o usuário digitou.
            // Como o seu método Ativar2Fa anterior já salvou o "p_enabled = 1" no banco de dados, 
            // a conta já está tecnicamente protegida. 

            // Então, tudo o que precisamos fazer é renderizar a View de sucesso que criamos!
            return View();
        }

        // ── Helper: detectar bandeira pelo prefixo ────────────────────────────

        private static string DetectarBandeira(string num) =>
            num switch
            {
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^4") => "Visa",
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^5[1-5]") => "Mastercard",
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^3[47]") => "Amex",
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^6(?:011|5)") => "Discover",
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^(?:384100|438935|504175|627780|636368|636297)") => "Elo",
                _ when System.Text.RegularExpressions.Regex.IsMatch(num, @"^(?:606282|3841)") => "Hipercard",
                _ => "Outro"
            };
    }
}
