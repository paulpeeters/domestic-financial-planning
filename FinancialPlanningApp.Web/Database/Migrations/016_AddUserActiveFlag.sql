SET @has_user_is_active := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND COLUMN_NAME = 'is_active'
);

SET @ddl_user_is_active := IF(
    @has_user_is_active = 0,
    'ALTER TABLE app_users ADD COLUMN is_active TINYINT(1) NOT NULL DEFAULT 1 AFTER is_global_admin',
    'SELECT 1'
);

PREPARE stmt_user_is_active FROM @ddl_user_is_active;
EXECUTE stmt_user_is_active;
DEALLOCATE PREPARE stmt_user_is_active;

SET @has_user_active_index := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND INDEX_NAME = 'ix_app_users_active'
);

SET @ddl_user_active_index := IF(
    @has_user_active_index = 0,
    'ALTER TABLE app_users ADD INDEX ix_app_users_active (is_active)',
    'SELECT 1'
);

PREPARE stmt_user_active_index FROM @ddl_user_active_index;
EXECUTE stmt_user_active_index;
DEALLOCATE PREPARE stmt_user_active_index;
