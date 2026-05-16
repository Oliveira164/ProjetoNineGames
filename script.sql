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