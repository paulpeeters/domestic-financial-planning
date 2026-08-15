CREATE TABLE IF NOT EXISTS app_users (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(256) NOT NULL UNIQUE,
    password_hash VARCHAR(1024) NOT NULL,
    created_utc DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS recurring_payment_templates (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    description VARCHAR(256) NOT NULL,
    display_order INT NOT NULL,
    periodicity VARCHAR(32) NOT NULL,
    payment_month TINYINT NULL,
    payment_day TINYINT NULL,
    payment_method VARCHAR(64) NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    normalized_monthly_amount DECIMAL(12,2) NOT NULL,
    active_from DATE NOT NULL,
    active_until DATE NULL,
    is_active BIT NOT NULL,
    created_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_templates_users FOREIGN KEY (user_id) REFERENCES app_users(id),
    INDEX ix_templates_user_active (user_id, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
