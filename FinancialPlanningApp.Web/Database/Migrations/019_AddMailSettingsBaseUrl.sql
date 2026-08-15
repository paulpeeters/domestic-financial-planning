SET @has_base_url := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'mail_settings' AND COLUMN_NAME = 'base_url'
);

SET @ddl_base_url := IF(@has_base_url = 0, 'ALTER TABLE mail_settings ADD COLUMN base_url VARCHAR(512) NULL AFTER from_email', 'SELECT 1');
PREPARE stmt FROM @ddl_base_url; EXECUTE stmt; DEALLOCATE PREPARE stmt;
