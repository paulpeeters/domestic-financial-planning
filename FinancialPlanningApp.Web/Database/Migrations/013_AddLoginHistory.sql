CREATE TABLE IF NOT EXISTS auth_login_attempts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    attempted_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    email VARCHAR(320) NULL,
    user_id BIGINT NULL,
    is_success TINYINT(1) NOT NULL,
    failure_reason VARCHAR(256) NULL,
    ip_address VARCHAR(64) NULL,
    user_agent VARCHAR(1024) NULL,
    CONSTRAINT fk_auth_login_attempts_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    INDEX ix_auth_login_attempts_attempted (attempted_utc),
    INDEX ix_auth_login_attempts_email (email),
    INDEX ix_auth_login_attempts_success (is_success)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
