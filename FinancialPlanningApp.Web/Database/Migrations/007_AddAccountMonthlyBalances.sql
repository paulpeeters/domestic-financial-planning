CREATE TABLE IF NOT EXISTS account_monthly_balances (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    account_number VARCHAR(64) NOT NULL,
    year INT NOT NULL,
    month INT NOT NULL,
    opening_balance DECIMAL(15,3) NULL,
    closing_balance DECIMAL(15,3) NOT NULL,
    source_reference VARCHAR(256) NULL,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_account_monthly_balances_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    UNIQUE KEY ux_account_monthly_balance_user_account_month (user_id, account_number, year, month)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

