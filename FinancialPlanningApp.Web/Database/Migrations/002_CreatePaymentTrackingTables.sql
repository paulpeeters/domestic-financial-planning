CREATE TABLE IF NOT EXISTS payment_executions (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    template_id BIGINT NULL,
    execution_date DATE NOT NULL,
    description VARCHAR(256) NOT NULL,
    payment_method VARCHAR(64) NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    source_type VARCHAR(32) NOT NULL,
    source_reference VARCHAR(256) NULL,
    notes VARCHAR(1024) NULL,
    created_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_payment_executions_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    CONSTRAINT fk_payment_executions_template FOREIGN KEY (template_id) REFERENCES recurring_payment_templates(id),
    INDEX ix_payment_executions_user_date (user_id, execution_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS payment_corrections (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    payment_execution_id BIGINT NOT NULL,
    correction_type VARCHAR(32) NOT NULL,
    amount_delta DECIMAL(12,2) NOT NULL,
    reason VARCHAR(512) NOT NULL,
    created_utc DATETIME(6) NOT NULL,
    CONSTRAINT fk_payment_corrections_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    CONSTRAINT fk_payment_corrections_execution FOREIGN KEY (payment_execution_id) REFERENCES payment_executions(id),
    INDEX ix_payment_corrections_execution (payment_execution_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
