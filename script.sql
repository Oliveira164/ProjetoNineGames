CREATE DATABASE IF NOT EXISTS bdloja_games;
USE bdloja_games;

-- ======================================================
-- CRIAÇÃO DAS TABELAS
-- ======================================================

-- Tabela de Categorias
CREATE TABLE categoria (
    id INT PRIMARY KEY AUTO_INCREMENT,
    nome VARCHAR(60) NOT NULL,
    descricao VARCHAR(200),
    criado_em TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Tabela de Usuários 
CREATE TABLE usuarios (
    id INT AUTO_INCREMENT PRIMARY KEY,
    nome VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    senha_hash VARCHAR(255) NOT NULL,
    two_factor_secret VARCHAR(255), -- Chave do Steam Guard (TOTP)
    two_factor_enabled BOOLEAN DEFAULT FALSE,
    role ENUM('Cliente', 'Funcionario', 'Admin') DEFAULT 'Cliente',
    ativo TINYINT(1) DEFAULT 1,
    criado_em TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela de Jogos
CREATE TABLE jogos (
    id INT AUTO_INCREMENT PRIMARY KEY,
    titulo VARCHAR(150) NOT NULL,
    descricao TEXT,
    preco DECIMAL(10, 2) NOT NULL,
    id_categoria INT,
    imagem_url VARCHAR(255),
    criado_em TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_produto_categoria FOREIGN KEY (id_categoria) REFERENCES categoria(id)
);

-- Tabela Biblioteca (Jogos que o utilizador já comprou)
CREATE TABLE biblioteca (
    usuario_id INT,
    jogo_id INT,
    data_aquisicao TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (usuario_id, jogo_id),
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
    FOREIGN KEY (jogo_id) REFERENCES jogos(id) ON DELETE CASCADE
);

-- Tabela Lista de Desejos (Wishlist)
CREATE TABLE lista_desejos (
    usuario_id INT,
    jogo_id INT,
    adicionado_em TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (usuario_id, jogo_id),
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
    FOREIGN KEY (jogo_id) REFERENCES jogos(id) ON DELETE CASCADE
);

-- Tabela de Vendas 
CREATE TABLE venda (
    id INT PRIMARY KEY AUTO_INCREMENT,
    id_usuario INT NOT NULL,
    data_hora TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    valor_total DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    forma_pagamento VARCHAR(30),
    status ENUM('Aberta', 'Finalizada', 'Cancelada') NOT NULL DEFAULT 'Aberta',
    CONSTRAINT fk_venda_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios(id)
);

-- Tabela de Itens da Venda 
CREATE TABLE venda_itens (
    id INT PRIMARY KEY AUTO_INCREMENT,
    id_venda INT NOT NULL,
    id_jogo INT NOT NULL,
    quantidade INT NOT NULL DEFAULT 1,
    preco_unitario DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_venda_itens_venda FOREIGN KEY (id_venda) REFERENCES venda(id),
    CONSTRAINT fk_venda_itens_jogo FOREIGN KEY (id_jogo) REFERENCES jogos(id)
);

-- ======================================================
-- INSERÇÕES PADRÃO (SEEDS)
-- ======================================================

-- Usuário admin para teste (senha: 12345)
INSERT IGNORE INTO usuarios (nome, email, senha_hash, role, ativo)
VALUES (
    'Administrador',
    'admin@ninegames.com',
    '$2b$10$8Y5Oj329TeEh8weYpJA6EOE39AA/BXVFOEUn1YOFC.sf1chUi4H8i',
    'Admin',
    1
);

-- ======================================================
-- STORED PROCEDURES: USUÁRIOS
-- ======================================================

DROP PROCEDURE IF EXISTS sp_usuario_obter_por_email;
DELIMITER $$
CREATE PROCEDURE sp_usuario_obter_por_email(IN p_email VARCHAR(180))
BEGIN
    SELECT id, nome, email, senha_hash, role, ativo,
           two_factor_enabled, two_factor_secret, criado_em
    FROM   usuarios
    WHERE  email = p_email
    LIMIT  1;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_usuario_obter_por_id;
DELIMITER $$
CREATE PROCEDURE sp_usuario_obter_por_id(IN p_id INT)
BEGIN
    SELECT id, nome, email, senha_hash, role, ativo,
           two_factor_enabled, two_factor_secret, criado_em
    FROM   usuarios
    WHERE  id = p_id
    LIMIT  1;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_usuario_criar;
DELIMITER $$
CREATE PROCEDURE sp_usuario_criar(
    IN p_nome       VARCHAR(120),
    IN p_email      VARCHAR(180),
    IN p_senha_hash VARCHAR(72),
    IN p_role       VARCHAR(30)
)
BEGIN
    INSERT INTO usuarios (nome, email, senha_hash, role)
    VALUES (p_nome, p_email, p_senha_hash, p_role);
    SELECT LAST_INSERT_ID() AS id;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_usuario_atualizar_2fa;
DELIMITER $$
CREATE PROCEDURE sp_usuario_atualizar_2fa(
    IN p_id               INT,
    IN p_enabled          TINYINT(1),
    IN p_two_factor_secret VARCHAR(64)
)
BEGIN
    UPDATE usuarios
    SET    two_factor_enabled = p_enabled,
           two_factor_secret  = p_two_factor_secret
    WHERE  id = p_id;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_usuario_listar;
DELIMITER $$
CREATE PROCEDURE sp_usuario_listar()
BEGIN
    SELECT id, nome, email, role, ativo, two_factor_enabled, criado_em
    FROM   usuarios
    ORDER BY nome;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_usuario_alterar_ativo;
DELIMITER $$
CREATE PROCEDURE sp_usuario_alterar_ativo(IN p_id INT, IN p_ativo TINYINT(1))
BEGIN
    UPDATE usuarios SET ativo = p_ativo WHERE id = p_id;
END$$
DELIMITER ;

-- ======================================================
-- STORED PROCEDURES: CATEGORIAS
-- ======================================================

DROP PROCEDURE IF EXISTS sp_categoria_listar;
DELIMITER $$
CREATE PROCEDURE sp_categoria_listar()
BEGIN
    SELECT id, nome, descricao, criado_em FROM categoria ORDER BY nome;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_categoria_obter;
DELIMITER $$
CREATE PROCEDURE sp_categoria_obter(IN p_id INT)
BEGIN
    SELECT id, nome, descricao, criado_em FROM categoria WHERE id = p_id LIMIT 1;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_categoria_criar;
DELIMITER $$
CREATE PROCEDURE sp_categoria_criar(IN p_nome VARCHAR(60), IN p_descricao VARCHAR(200))
BEGIN
    INSERT INTO categoria (nome, descricao) VALUES (p_nome, p_descricao);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_categoria_atualizar;
DELIMITER $$
CREATE PROCEDURE sp_categoria_atualizar(IN p_id INT, IN p_nome VARCHAR(60), IN p_descricao VARCHAR(200))
BEGIN
    UPDATE categoria SET nome = p_nome, descricao = p_descricao WHERE id = p_id;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_categoria_excluir;
DELIMITER $$
CREATE PROCEDURE sp_categoria_excluir(IN p_id INT)
BEGIN
    IF EXISTS (SELECT 1 FROM jogos WHERE id_categoria = p_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Não é possível excluir: categoria em uso por jogos.';
    END IF;
    DELETE FROM categoria WHERE id = p_id;
END$$
DELIMITER ;

-- ======================================================
-- STORED PROCEDURES: JOGOS
-- ======================================================

DROP PROCEDURE IF EXISTS sp_jogo_listar;
DELIMITER $$
CREATE PROCEDURE sp_jogo_listar(IN p_categoria VARCHAR(60), IN p_busca VARCHAR(150))
BEGIN
    SELECT j.id, j.titulo, j.descricao, j.preco, j.imagem_url, c.nome AS categoria
    FROM   jogos j
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  (p_categoria IS NULL OR c.nome = p_categoria)
      AND  (p_busca     IS NULL OR j.titulo LIKE CONCAT('%', p_busca, '%'))
    ORDER BY j.titulo;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_jogo_obter;
DELIMITER $$
CREATE PROCEDURE sp_jogo_obter(IN p_id INT)
BEGIN
    SELECT j.id, j.titulo, j.descricao, j.preco, j.imagem_url,
           c.nome AS categoria, j.id_categoria
    FROM   jogos j
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  j.id = p_id LIMIT 1;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_jogo_categorias;
DELIMITER $$
CREATE PROCEDURE sp_jogo_categorias()
BEGIN
    SELECT DISTINCT c.nome AS categoria
    FROM jogos j
    JOIN categoria c ON c.id = j.id_categoria
    ORDER BY c.nome;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_jogo_criar;
DELIMITER $$
CREATE PROCEDURE sp_jogo_criar(
    IN p_titulo      VARCHAR(150),
    IN p_descricao   TEXT,
    IN p_preco       DECIMAL(10,2),
    IN p_categoria   VARCHAR(60),
    IN p_imagem_url  VARCHAR(255)
)
BEGIN
    DECLARE v_cat_id INT DEFAULT NULL;
    IF p_categoria IS NOT NULL THEN
        SELECT id INTO v_cat_id FROM categoria WHERE nome = p_categoria LIMIT 1;
    END IF;
    INSERT INTO jogos (titulo, descricao, preco, id_categoria, imagem_url)
    VALUES (p_titulo, p_descricao, p_preco, v_cat_id, p_imagem_url);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_jogo_atualizar;
DELIMITER $$
CREATE PROCEDURE sp_jogo_atualizar(
    IN p_id          INT,
    IN p_titulo      VARCHAR(150),
    IN p_descricao   TEXT,
    IN p_preco       DECIMAL(10,2),
    IN p_categoria   VARCHAR(60),
    IN p_imagem_url  VARCHAR(255)
)
BEGIN
    DECLARE v_cat_id INT DEFAULT NULL;
    IF p_categoria IS NOT NULL THEN
        SELECT id INTO v_cat_id FROM categoria WHERE nome = p_categoria LIMIT 1;
    END IF;
    UPDATE jogos
    SET titulo = p_titulo, descricao = p_descricao, preco = p_preco,
        id_categoria = v_cat_id, imagem_url = p_imagem_url
    WHERE id = p_id;
END$$
DELIMITER ; 

DROP PROCEDURE IF EXISTS sp_jogo_excluir;
DELIMITER $$
CREATE PROCEDURE sp_jogo_excluir(IN p_id INT)
BEGIN
    -- Verifica se o jogo já possui vendas registradas
    IF EXISTS (SELECT 1 FROM venda_itens WHERE id_jogo = p_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Não é possível excluir: o jogo já possui vendas registradas.';
    END IF;

    -- Deleta o jogo (Wishlist e Biblioteca serão apagados automaticamente devido ao ON DELETE CASCADE)
    DELETE FROM jogos WHERE id = p_id;
END$$
DELIMITER ;

-- ===========================================================
-- VENDA
-- ────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_venda_criar;
DELIMITER $$
CREATE PROCEDURE sp_venda_criar(
    IN  p_id_usuario      INT,
    IN  p_valor_total     DECIMAL(12,2),
    IN  p_forma_pagamento VARCHAR(30),
    OUT p_id_gerado       INT
)
BEGIN
    INSERT INTO venda (id_usuario, valor_total, forma_pagamento, status)
    VALUES (p_id_usuario, p_valor_total, p_forma_pagamento, 'Aberta');
    SET p_id_gerado = LAST_INSERT_ID();
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_venda_adicionar_item;
DELIMITER $$
CREATE PROCEDURE sp_venda_adicionar_item(
    IN p_id_venda        INT,
    IN p_id_jogo         INT,
    IN p_quantidade      INT,
    IN p_preco_unitario  DECIMAL(10,2)
)
BEGIN
    -- Apenas insere o item na venda, sem validação ou baixa de estoque
    INSERT INTO venda_itens (id_venda, id_jogo, quantidade, preco_unitario)
    VALUES (p_id_venda, p_id_jogo, p_quantidade, p_preco_unitario);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_venda_finalizar;
DELIMITER $$
CREATE PROCEDURE sp_venda_finalizar(IN p_id_venda INT)
BEGIN
    DECLARE v_usuario INT;

    -- Marca venda como finalizada
    UPDATE venda SET status = 'Finalizada' WHERE id = p_id_venda;

    -- Registra os jogos na biblioteca do usuário (ignora duplicatas)
    SELECT id_usuario INTO v_usuario FROM venda WHERE id = p_id_venda;

    INSERT IGNORE INTO biblioteca (usuario_id, jogo_id)
    SELECT v_usuario, id_jogo FROM venda_itens WHERE id_venda = p_id_venda;
END$$
DELIMITER ;

-- ────────────────────────────────────────────────────────────
-- BIBLIOTECA
-- ────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_biblioteca_listar;
DELIMITER $$
CREATE PROCEDURE sp_biblioteca_listar(IN p_id_usuario INT)
BEGIN
    SELECT j.id, j.titulo, j.descricao, j.preco, j.imagem_url,
           c.nome AS categoria, b.data_aquisicao
    FROM   biblioteca b
    JOIN   jogos      j ON j.id = b.jogo_id
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  b.usuario_id = p_id_usuario
    ORDER BY b.data_aquisicao DESC;
END$$
DELIMITER ;

-- NOVO: Stored Procedure criada para uso na vitrine (JogoController)
DROP PROCEDURE IF EXISTS sp_biblioteca_obter_ids;
DELIMITER $$
CREATE PROCEDURE sp_biblioteca_obter_ids(IN p_id_usuario INT)
BEGIN
    SELECT jogo_id 
    FROM   biblioteca 
    WHERE  usuario_id = p_id_usuario;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_biblioteca_listar_filtro;
DELIMITER $$
CREATE PROCEDURE sp_biblioteca_listar_filtro(
    IN p_id_usuario INT,
    IN p_categoria  VARCHAR(60),   -- NULL = todas
    IN p_busca      VARCHAR(150)   -- NULL = sem filtro de texto
)
BEGIN
    SELECT j.id, j.titulo, j.descricao, j.preco, j.imagem_url,
           c.nome AS categoria, b.data_aquisicao
    FROM   biblioteca b
    JOIN   jogos      j ON j.id = b.jogo_id
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  b.usuario_id = p_id_usuario
      AND  (p_categoria IS NULL OR c.nome = p_categoria)
      AND  (p_busca     IS NULL OR j.titulo LIKE CONCAT('%', p_busca, '%'))
    ORDER BY b.data_aquisicao DESC;
END$$
DELIMITER ;

-- SP que retorna apenas as categorias que o usuário tem na biblioteca
DROP PROCEDURE IF EXISTS sp_biblioteca_categorias;
DELIMITER $$
CREATE PROCEDURE sp_biblioteca_categorias(IN p_id_usuario INT)
BEGIN
    SELECT DISTINCT c.nome AS categoria
    FROM   biblioteca b
    JOIN   jogos      j ON j.id = b.jogo_id
    JOIN   categoria  c ON c.id = j.id_categoria
    WHERE  b.usuario_id = p_id_usuario
    ORDER BY c.nome;
END$$
DELIMITER ;

-- ────────────────────────────────────────────────────────────
-- WISHLIST
-- ────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_wishlist_listar;
DELIMITER $$
CREATE PROCEDURE sp_wishlist_listar(IN p_id_usuario INT)
BEGIN
    SELECT j.id, j.titulo, j.preco, j.imagem_url, c.nome AS categoria
    FROM   lista_desejos ld
    JOIN   jogos j ON j.id = ld.jogo_id
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  ld.usuario_id = p_id_usuario
    ORDER BY ld.adicionado_em DESC;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_wishlist_adicionar;
DELIMITER $$
CREATE PROCEDURE sp_wishlist_adicionar(IN p_id_usuario INT, IN p_id_jogo INT)
BEGIN
    INSERT IGNORE INTO lista_desejos (usuario_id, jogo_id) VALUES (p_id_usuario, p_id_jogo);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_wishlist_remover;
DELIMITER $$
CREATE PROCEDURE sp_wishlist_remover(IN p_id_usuario INT, IN p_id_jogo INT)
BEGIN
    DELETE FROM lista_desejos WHERE usuario_id = p_id_usuario AND jogo_id = p_id_jogo;
END$$
DELIMITER ;

-- ============================================================
-- Dashboard Admin — Stored Procedures
-- ============================================================

-- ── 1. KPIs gerais (cards do topo) ──────────────────────────
DROP PROCEDURE IF EXISTS sp_dashboard_kpis;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_kpis()
BEGIN
    SELECT
        -- Receita total (vendas finalizadas)
        COALESCE(SUM(CASE WHEN status = 'Finalizada' THEN valor_total END), 0) AS receita_total,

        -- Total de vendas finalizadas
        COUNT(CASE WHEN status = 'Finalizada' THEN 1 END) AS total_vendas,

        -- Ticket médio
        COALESCE(AVG(CASE WHEN status = 'Finalizada' THEN valor_total END), 0) AS ticket_medio,

        -- Total de usuários ativos
        (SELECT COUNT(*) FROM usuarios WHERE ativo = 1 AND role = 'Cliente') AS total_clientes,

        -- Total de jogos cadastrados
        (SELECT COUNT(*) FROM jogos) AS total_jogos,

        -- Vendas do mês atual
        COALESCE(SUM(
            CASE WHEN status = 'Finalizada'
                  AND MONTH(data_hora) = MONTH(CURRENT_DATE())
                  AND YEAR(data_hora)  = YEAR(CURRENT_DATE())
            THEN valor_total END
        ), 0) AS receita_mes,

        -- Vendas do mês anterior (para calcular variação)
        COALESCE(SUM(
            CASE WHEN status = 'Finalizada'
                  AND MONTH(data_hora) = MONTH(DATE_SUB(CURRENT_DATE(), INTERVAL 1 MONTH))
                  AND YEAR(data_hora)  = YEAR(DATE_SUB(CURRENT_DATE(), INTERVAL 1 MONTH))
            THEN valor_total END
        ), 0) AS receita_mes_anterior

    FROM venda;
END$$
DELIMITER ;

-- ── 2. Últimas vendas (tabela recente) ───────────────────────
DROP PROCEDURE IF EXISTS sp_dashboard_ultimas_vendas;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_ultimas_vendas(IN p_limite INT)
BEGIN
    SELECT
        v.id,
        u.nome        AS cliente,
        u.email,
        v.valor_total,
        v.forma_pagamento,
        v.status,
        v.data_hora,
        COUNT(vi.id)  AS qtd_itens
    FROM venda v
    JOIN usuarios u ON u.id = v.id_usuario
    JOIN venda_itens vi ON vi.id_venda = v.id
    GROUP BY v.id, u.nome, u.email, v.valor_total, v.forma_pagamento, v.status, v.data_hora
    ORDER BY v.data_hora DESC
    LIMIT p_limite;
END$$
DELIMITER ;

-- ── 3. Jogos mais vendidos ────────────────────────────────────
DROP PROCEDURE IF EXISTS sp_dashboard_top_jogos;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_top_jogos(IN p_limite INT)
BEGIN
    SELECT
        j.id,
        j.titulo,
        j.imagem_url,
        c.nome                  AS categoria,
        j.preco,
        SUM(vi.quantidade)      AS total_vendido,
        SUM(vi.quantidade * vi.preco_unitario) AS receita_gerada,
        COUNT(DISTINCT v.id_usuario)           AS compradores
    FROM venda_itens vi
    JOIN venda v  ON v.id  = vi.id_venda  AND v.status = 'Finalizada'
    JOIN jogos j  ON j.id  = vi.id_jogo
    LEFT JOIN categoria c ON c.id = j.id_categoria
    GROUP BY j.id, j.titulo, j.imagem_url, c.nome, j.preco
    ORDER BY total_vendido DESC
    LIMIT p_limite;
END$$
DELIMITER ;

-- ── 4. Receita por mês (últimos 6 meses) ────────────────────
DROP PROCEDURE IF EXISTS sp_dashboard_receita_mensal;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_receita_mensal()
BEGIN
    SELECT
        DATE_FORMAT(data_hora, '%Y-%m') AS mes,
        DATE_FORMAT(data_hora, '%b/%Y') AS mes_label,
        COALESCE(SUM(valor_total), 0)   AS receita,
        COUNT(*)                         AS quantidade
    FROM venda
    WHERE status    = 'Finalizada'
      AND data_hora >= DATE_SUB(CURRENT_DATE(), INTERVAL 6 MONTH)
    GROUP BY DATE_FORMAT(data_hora, '%Y-%m'), DATE_FORMAT(data_hora, '%b/%Y')
    ORDER BY mes ASC;
END$$
DELIMITER ;

-- ── 5. Vendas por forma de pagamento ────────────────────────
DROP PROCEDURE IF EXISTS sp_dashboard_por_pagamento;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_por_pagamento()
BEGIN
    SELECT
        COALESCE(forma_pagamento, 'Não informado') AS forma,
        COUNT(*)                                    AS quantidade,
        SUM(valor_total)                            AS total
    FROM venda
    WHERE status = 'Finalizada'
    GROUP BY forma_pagamento
    ORDER BY quantidade DESC;
END$$
DELIMITER ;

-- ── 6. Novos usuários por mês (últimos 6 meses) ─────────────
DROP PROCEDURE IF EXISTS sp_dashboard_novos_usuarios;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_novos_usuarios()
BEGIN
    SELECT
        DATE_FORMAT(criado_em, '%Y-%m') AS mes,
        DATE_FORMAT(criado_em, '%b/%Y') AS mes_label,
        COUNT(*)                         AS quantidade
    FROM usuarios
    WHERE role     = 'Cliente'
      AND criado_em >= DATE_SUB(CURRENT_DATE(), INTERVAL 6 MONTH)
    GROUP BY DATE_FORMAT(criado_em, '%Y-%m'), DATE_FORMAT(criado_em, '%b/%Y')
    ORDER BY mes ASC;
END$$
DELIMITER ;

-- ── 7. Jogos comprados por usuário (histórico detalhado) ─────
DROP PROCEDURE IF EXISTS sp_dashboard_vendas_detalhe;
DELIMITER $$
CREATE PROCEDURE sp_dashboard_vendas_detalhe(IN p_limite INT)
BEGIN
    SELECT
        v.id          AS venda_id,
        v.data_hora,
        u.nome        AS cliente,
        u.email,
        j.titulo      AS jogo,
        c.nome        AS categoria,
        vi.quantidade,
        vi.preco_unitario,
        (vi.quantidade * vi.preco_unitario) AS subtotal,
        v.forma_pagamento,
        v.status
    FROM venda_itens vi
    JOIN venda    v ON v.id  = vi.id_venda
    JOIN usuarios u ON u.id  = v.id_usuario
    JOIN jogos    j ON j.id  = vi.id_jogo
    LEFT JOIN categoria c ON c.id = j.id_categoria
    ORDER BY v.data_hora DESC
    LIMIT p_limite;
END$$
DELIMITER ;

-- Retorna jogos da mesma categoria, excluindo o jogo atual
DROP PROCEDURE IF EXISTS sp_jogo_sugestoes;
DELIMITER $$
CREATE PROCEDURE sp_jogo_sugestoes(
    IN p_id_jogo    INT,
    IN p_categoria  VARCHAR(60),
    IN p_limite     INT
)
BEGIN
    SELECT j.id, j.titulo, j.preco, j.imagem_url, c.nome AS categoria
    FROM   jogos j
    LEFT JOIN categoria c ON c.id = j.id_categoria
    WHERE  c.nome = p_categoria
      AND  j.id  != p_id_jogo
    ORDER BY RAND()
    LIMIT p_limite;
END$$
DELIMITER ;
