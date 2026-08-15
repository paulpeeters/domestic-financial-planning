CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    token_hash VARCHAR(128) NOT NULL,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_utc DATETIME(6) NOT NULL,
    used_utc DATETIME(6) NULL,
    request_ip VARCHAR(64) NULL,
    user_agent VARCHAR(512) NULL,
    CONSTRAINT fk_password_reset_tokens_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    UNIQUE KEY ux_password_reset_tokens_hash (token_hash),
    INDEX ix_password_reset_tokens_user_active (user_id, used_utc, expires_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
