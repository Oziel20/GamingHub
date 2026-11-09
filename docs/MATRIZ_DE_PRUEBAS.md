# Matriz de pruebas

**Ninguna prueba fue ejecutada**: el entorno donde se generó el proyecto no tiene .NET SDK ni Windows. Todas figuran como BLOCKED hasta que se ejecuten con `dotnet test` o el Explorador de pruebas de Visual Studio. Rellena «Resultado obtenido» y cambia el estado tras ejecutarlas.

| ID | Módulo | Descripción (prueba automatizada) | Entrada | Resultado esperado | Resultado obtenido | Estado | Severidad | Evidencia | Observaciones |
|---|---|---|---|---|---|---|---|---|---|
| T-001 | Hasher | Hash is not plaintext and verifies | ver `HasherTests.Hash_is_not_plaintext_and_verifies` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-002 | Hasher | Salts are unique | ver `HasherTests.Salts_are_unique` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-003 | Hasher | Malformed hash never verifies | ver `HasherTests.Malformed_hash_never_verifies` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Theory |
| T-004 | Hasher | Weak iteration count rejected | ver `HasherTests.Weak_iteration_count_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-005 | Hasher | Tokens are unique and hash differs | ver `HasherTests.Tokens_are_unique_and_hash_differs` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-006 | JsonFileStore | Creates directory and file when missing | ver `JsonFileStoreTests.Creates_directory_and_file_when_missing` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-007 | JsonFileStore | Persists across instances | ver `JsonFileStoreTests.Persists_across_instances` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-008 | JsonFileStore | Empty file is handled | ver `JsonFileStoreTests.Empty_file_is_handled` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-009 | JsonFileStore | Corrupt file is quarantined and backup restored | ver `JsonFileStoreTests.Corrupt_file_is_quarantined_and_backup_restored` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-010 | JsonFileStore | Corrupt file without backup starts empty | ver `JsonFileStoreTests.Corrupt_file_without_backup_starts_empty` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-011 | JsonFileStore | Missing properties use defaults | ver `JsonFileStoreTests.Missing_properties_use_defaults` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-012 | JsonFileStore | Leftover tmp is recovered when main missing | ver `JsonFileStoreTests.Leftover_tmp_is_recovered_when_main_missing` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-013 | JsonFileStore | Concurrent updates do not lose writes | ver `JsonFileStoreTests.Concurrent_updates_do_not_lose_writes` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-014 | JsonFileStore | Failed update does not corrupt state | ver `JsonFileStoreTests.Failed_update_does_not_corrupt_state` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-015 | JsonFileStore | Uncommitted update is not written | ver `JsonFileStoreTests.Uncommitted_update_is_not_written` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-016 | JsonFileStore | Large file loads quickly | ver `JsonFileStoreTests.Large_file_loads_quickly` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-017 | AuthService | Register valid succeeds and first user is admin | ver `AuthServiceTests.Register_valid_succeeds_and_first_user_is_admin` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-018 | AuthService | Register rejects duplicates case insensitively | ver `AuthServiceTests.Register_rejects_duplicates_case_insensitively` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-019 | AuthService | Register rejects mismatch terms and ages | ver `AuthServiceTests.Register_rejects_mismatch_terms_and_ages` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-020 | AuthService | Register rejects 65 char password even if ui is bypassed | ver `AuthServiceTests.Register_rejects_65_char_password_even_if_ui_is_bypassed` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-021 | AuthService | Login success wrong password and generic message | ver `AuthServiceTests.Login_success_wrong_password_and_generic_message` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-022 | AuthService | Login rejects oversized input | ver `AuthServiceTests.Login_rejects_oversized_input` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-023 | AuthService | Brute force triggers temporary lock that expires | ver `AuthServiceTests.Brute_force_triggers_temporary_lock_that_expires` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-024 | AuthService | Logout invalidates session and sessions expire | ver `AuthServiceTests.Logout_invalidates_session_and_sessions_expire` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-025 | AuthService | Recovery token is single use and revokes sessions | ver `AuthServiceTests.Recovery_token_is_single_use_and_revokes_sessions` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-026 | AuthService | Recovery token expires and weak password does not burn it | ver `AuthServiceTests.Recovery_token_expires_and_weak_password_does_not_burn_it` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-027 | AuthService | Recovery requests are rate limited | ver `AuthServiceTests.Recovery_requests_are_rate_limited` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-028 | AuthService | Services require authentication | ver `AuthServiceTests.Services_require_authentication` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-029 | AuthService | Admin operations are denied to normal users | ver `AuthServiceTests.Admin_operations_are_denied_to_normal_users` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-030 | LibraryService | Add search sort and remove | ver `LibraryServiceTests.Add_search_sort_and_remove` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-031 | LibraryService | Search boundaries are enforced by the service | ver `LibraryServiceTests.Search_boundaries_are_enforced_by_the_service` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-032 | LibraryService | Duplicates outside roots and fake exes are rejected | ver `LibraryServiceTests.Duplicates_outside_roots_and_fake_exes_are_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-033 | LibraryService | Paging limits results per page | ver `LibraryServiceTests.Paging_limits_results_per_page` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-034 | LibraryService | Libraries are isolated between users | ver `LibraryServiceTests.Libraries_are_isolated_between_users` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-035 | StoreService | Catalog cart checkout and duplicate protection | ver `StoreServiceTests.Catalog_cart_checkout_and_duplicate_protection` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-036 | StoreService | Store search enforces limits | ver `StoreServiceTests.Store_search_enforces_limits` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-037 | SocialService | Self and duplicate requests are rejected | ver `SocialServiceTests.Self_and_duplicate_requests_are_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-038 | SocialService | Only the target can accept a request | ver `SocialServiceTests.Only_the_target_can_accept_a_request` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-039 | SocialService | Messaging rules | ver `SocialServiceTests.Messaging_rules` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-040 | SocialService | Messages are rate limited | ver `SocialServiceTests.Messages_are_rate_limited` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-041 | SocialService | Blocked users cannot message or read | ver `SocialServiceTests.Blocked_users_cannot_message_or_read` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-042 | PasswordValidator | Rejects password containing username | ver `PasswordValidatorTests.Rejects_password_containing_username` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-043 | PasswordValidator | Rejects password equal to username | ver `PasswordValidatorTests.Rejects_password_equal_to_username` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-044 | PasswordValidator | Accepts strong password | ver `PasswordValidatorTests.Accepts_strong_password` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-045 | UserNameValidator | Accepts letters digits underscore | ver `UserNameValidatorTests.Accepts_letters_digits_underscore` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-046 | UserNameValidator | Rejects huge input | ver `UserNameValidatorTests.Rejects_huge_input` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-047 | SearchValidator | Null is invalid | ver `SearchValidatorTests.Null_is_invalid` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-048 | SearchValidator | Empty means show all | ver `SearchValidatorTests.Empty_means_show_all` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Theory |
| T-049 | SearchValidator | Trims and keeps inner spaces | ver `SearchValidatorTests.Trims_and_keeps_inner_spaces` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-050 | SearchValidator | Rejects control chars | ver `SearchValidatorTests.Rejects_control_chars` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-051 | SearchValidator | Single char after trim is invalid | ver `SearchValidatorTests.Single_char_after_trim_is_invalid` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-052 | EmailAndTextValidator | Email 254 ok 255 rejected | ver `EmailAndTextValidatorTests.Email_254_ok_255_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-053 | EmailAndTextValidator | Message limits | ver `EmailAndTextValidatorTests.Message_limits` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-054 | FileValidator | Valid exe inside root | ver `FileValidatorTests.Valid_exe_inside_root` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-055 | FileValidator | Exe outside root rejected | ver `FileValidatorTests.Exe_outside_root_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-056 | FileValidator | Traversal rejected | ver `FileValidatorTests.Traversal_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-057 | FileValidator | Wrong extension rejected | ver `FileValidatorTests.Wrong_extension_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-058 | FileValidator | Fake exe content rejected | ver `FileValidatorTests.Fake_exe_content_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-059 | FileValidator | Missing file rejected | ver `FileValidatorTests.Missing_file_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-060 | FileValidator | Valid png and jpg | ver `FileValidatorTests.Valid_png_and_jpg` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-061 | FileValidator | Renamed exe as png rejected | ver `FileValidatorTests.Renamed_exe_as_png_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |
| T-062 | FileValidator | Oversized image rejected | ver `FileValidatorTests.Oversized_image_rejected` | La aserción de la prueba se cumple | — | BLOCKED (no ejecutada) | — | — | Fact |

## Pruebas manuales de interfaz (no automatizadas, no ejecutadas)

| ID | Módulo | Descripción | Estado |
|---|---|---|---|
| UI-01 | Interfaz | Navegación entre todas las páginas | BLOCKED (no ejecutada) |
| UI-02 | Interfaz | Todos los botones ejecutan su acción | BLOCKED (no ejecutada) |
| UI-03 | Interfaz | Escalado/resoluciones y NavigationView adaptable | BLOCKED (no ejecutada) |
| UI-04 | Interfaz | Navegación con teclado y Enter en buscadores | BLOCKED (no ejecutada) |
| UI-05 | Interfaz | TextBox de búsqueda no admite más de 100 caracteres | BLOCKED (no ejecutada) |
| UI-06 | Interfaz | PasswordBox no admite más de 64 caracteres | BLOCKED (no ejecutada) |
| UI-07 | Interfaz | Indicador de coincidencia de contraseñas en tiempo real | BLOCKED (no ejecutada) |
| UI-08 | Interfaz | Selector de archivos para .exe, avatar y carpeta | BLOCKED (no ejecutada) |
| UI-09 | Interfaz | Cierre de sesión y expiración de sesión (latido de 60 s) | BLOCKED (no ejecutada) |
| UI-10 | Interfaz | Accesibilidad (lector de pantalla, contraste) | BLOCKED (no ejecutada) |
