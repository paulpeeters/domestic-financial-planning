SET @has_display_first_name := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_tenants' AND COLUMN_NAME = 'display_first_name'
);

SET @ddl_display_first_name := IF(@has_display_first_name = 0, 'ALTER TABLE user_tenants ADD COLUMN display_first_name VARCHAR(64) NULL AFTER is_active', 'SELECT 1');
PREPARE stmt FROM @ddl_display_first_name; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_display_last_name := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_tenants' AND COLUMN_NAME = 'display_last_name'
);

SET @ddl_display_last_name := IF(@has_display_last_name = 0, 'ALTER TABLE user_tenants ADD COLUMN display_last_name VARCHAR(64) NULL AFTER display_first_name', 'SELECT 1');
PREPARE stmt FROM @ddl_display_last_name; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @has_display_avatar_url := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_tenants' AND COLUMN_NAME = 'display_avatar_url'
);

SET @ddl_display_avatar_url := IF(@has_display_avatar_url = 0, 'ALTER TABLE user_tenants ADD COLUMN display_avatar_url VARCHAR(512) NULL AFTER display_last_name', 'SELECT 1');
PREPARE stmt FROM @ddl_display_avatar_url; EXECUTE stmt; DEALLOCATE PREPARE stmt;
