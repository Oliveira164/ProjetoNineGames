CREATE DATABASE IF NOT EXISTS bdloja_games;
USE bdloja_games;

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
    role ENUM('Cliente', 'Funcionario', 'Admin') DEFAULT 'Cliente', -- Ajustado para incluir clientes
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

-- Tabela Biblioteca 
-- Representa os jogos que o utilizador já comprou.
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
    CONSTRAINT fk_venda_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios(id) -- Corrigido de 'usuario' para 'usuarios'
);

-- Tabela de Itens da Venda 
CREATE TABLE venda_itens (
    id INT PRIMARY KEY AUTO_INCREMENT,
    id_venda INT NOT NULL,
    id_jogo INT NOT NULL,
    quantidade INT NOT NULL DEFAULT 1,
    preco_unitario DECIMAL(10,2) NOT NULL, -- Corrigido para acompanhar jogos.preco
    CONSTRAINT fk_venda_itens_venda FOREIGN KEY (id_venda) REFERENCES venda(id),
    CONSTRAINT fk_venda_itens_jogo FOREIGN KEY (id_jogo) REFERENCES jogos(id)
);

-- Stored Procedures

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

-- Ativa/desativa 2FA e grava o secret gerado pelo sistema
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

-- Usuário admin para teste (senha: Admin@123)
INSERT IGNORE INTO usuarios (nome, email, senha_hash, role, ativo)
VALUES (
    'Administrador',
    'admin@ninegames.com',
    '$2a$12$KIX4zqMb9I5ZvQEVWHYFheVn/lJYBuJBQ5kbPjTCEdS3jy7W4qmMy',
    'Admin',
    1
);