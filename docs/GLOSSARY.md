# Glossary of the user interface texts

The application is translated into 11 languages. The same domain term must be translated **the same way in every window** of a language: this document lists the
canonical translation of the 59 terms that matter. **Every new or changed string must use these terms** (see [CONVENTIONS.md](CONVENTIONS.md) and the guide
"A localized string" in [HOW_TO.md](HOW_TO.md)).

This file is **generated** from [`FalloutScriptStudio/Localization/Glossary.json`](../FalloutScriptStudio/Localization/Glossary.json) by `GlossaryTests`; do not edit it by hand.
To regenerate it after changing the JSON run the tests with the environment variable `FSS_WRITE_GLOSSARY=1`.

## How the glossary is enforced

`GlossaryTests` (project `FalloutScriptStudio.Tests`, no UI) read every string dictionary and, for every key whose **English** text contains a term below as a whole word,
check that the same key of each other language contains one of the *accepted forms* of the canonical translation (inflected forms, plurals and abbreviations are listed in the JSON).
The check also fails when a text uses a *forbidden synonym* of a term, when the English texts use a forbidden wording, and when a language uses a text that is excluded for it
(for example European Portuguese words: the Portuguese texts are Brazilian). The findings are reported all at once with the key, the language and the expected term.

* **Fix the translation first.** The allow-list `FalloutScriptStudio.Tests/Localization/GlossaryExceptions.json` is only for texts in which the term is legitimately not used
  (idiomatic phrase, term not translated there, grammatical restructuring). Each entry names a key, a language and a term and has a one-line justification; an entry that
  silences nothing is reported as stale and the list has a hard size limit.
* **A new term** goes into `Glossary.json` (English wordings, canonical translation and accepted forms per language, notes). Check the existing texts with the tests before you commit.
* **Matching rules** (also stored under `matching` in the JSON):
  * Texts are compared in lower case. Placeholders such as {0}, file names (scripts.lst, parser.dll, .ssl), underscores (access keys) and the phrases of "ignore" are removed first.
  * English: a term applies to a key when one of its "en" variants occurs as a whole word (case-insensitive) in the English text; text between single or double quotes without a blank (such as 'Node') is skipped.
  * Other languages: the text must contain one of the "accept" fragments (plain substrings, so that inflected forms and German compounds match). A fragment that starts with ^ must start at a word start, one that ends with $ must end a word.
  * "forbid" fragments (same syntax) must not occur in a text where the term applies. "forbiddenEverywhere" fragments must not occur in any text of the language.
  * "enForbid": English wordings that must not be used (the English texts are checked too).

## Terms

The tables show the canonical translation. Where the canonical form is the one of an inflected language (cs, pl, ru: nominative singular; de: infinitive or nominative; sv: indefinite form),
the other cases and the plural are accepted by the test and must be used with the right grammar. Two forms separated by a slash are two contexts of the term (for example noun and verb).

### Fallout and SSL

| English | cs | de | es | fr | it | pl | pt | ru | sv | zh |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Script** | skript | Skript | script | script | script | skrypt | script | скрипт | skript | 脚本 |
| **Procedure** | procedura | Prozedur | procedimiento | procédure | procedura | procedura | procedimento | процедура | procedur | 过程 |
| **Function** | funkce | Funktion | función | fonction | funzione | funkcja | função | функция | funktion | 函数 |
| **Variable** | proměnná | Variable | variable | variable | variabile | zmienna | variável | переменная | variabel | 变量 |
| **Macro** | makro | Makro | macro | macro | macro | makro | macro | макрос | makro | 宏 |
| **Definition (define)** | definice | Definition | definición | définition | definizione | definicja | definição | определение | definition | 定义 |
| **Declaration** | deklarace | Deklaration | declaración | déclaration | dichiarazione | deklaracja | declaração | объявление | deklaration | 声明 |
| **Header (file)** | hlavičkový soubor | Header | encabezado | en-tête | intestazione | plik nagłówkowy | cabeçalho | заголовочный файл | huvudfil | 头文件 |
| **Include** | vložený soubor / include | einbinden / Include | incluir / include | inclure / include | includere / include | dołączyć / include | incluir / include | подключить / include | inkludera / include | 包含 |
| **Message (file)** | zpráva | Nachricht | mensaje | message | messaggio | komunikat | mensagem | сообщение | meddelande | 消息 |
| **Dialogue** | dialog | Dialog | diálogo | dialogue | dialogo | dialog | diálogo | диалог | dialog | 对话 |
| **Node** | uzel | Knoten | nodo | nœud | nodo | węzeł | nó | узел | nod | 节点 |
| **Flowchart** | vývojový diagram | Flussdiagramm | diagrama de flujo | organigramme | diagramma di flusso | schemat blokowy | fluxograma | блок-схема | flödesschema | 流程图 |
| **Player** | hráč | Spieler | jugador | joueur | giocatore | gracz | jogador | игрок | spelare | 玩家 |
| **Opcode** | operační kód | Opcode | opcode | opcode | opcode | opkod | opcode | опкод | opkod | 操作码 |
| **Register** | registrovat | registrieren | registrar | inscrire | registrare | zarejestrować | registrar | зарегистрировать | registrera | 注册 |
| **Template** | šablona | Vorlage | plantilla | modèle | modello | szablon | modelo | шаблон | mall | 模板 |
| **Archive** | archiv | Archiv | archivo | archive | archivio | archiwum | arquivo | архив | arkiv | 档案 |

Usage notes:

* **Script**: An SSL source file (.ssl) or its compiled form (.int). Keep the English loan word where the language already does ("script" in es/fr/it/pt); never "program" or "file" alone. English wordings that trigger the check: `script`, `scripts`.
* **Procedure**: An SSL procedure (the unit of code; a dialogue node is a procedure). Never "function" (that is a built-in/engine function) and never "subroutine". English wordings that trigger the check: `procedure`, `procedures`. Do not use: es `subrutina`; zh `程序`, `存储过程`.
* **Function**: An engine/sfall function or a user code snippet of the function tree. Not the same as a procedure. English wordings that trigger the check: `function`, `functions`. Do not use: zh `功能`.
* **Variable**: A script, local, argument or global variable. English wordings that trigger the check: `variable`, `variables`.
* **Macro**: A #define macro of the preprocessor. English wordings that trigger the check: `macro`, `macros`.
* **Definition (define)**: A #define (preprocessor definition) or "where something is defined" (Go to definition). Not "declaration". English wordings that trigger the check: `definition`, `definitions`, `define`, `defines`, `defined`.
* **Declaration**: Where a procedure or variable is declared (Go to declaration). English wordings that trigger the check: `declaration`, `declarations`, `declared`, `declare`.
* **Header (file)**: A header file (*.h) with macros, procedures and variables shared between scripts. Spell the file kind out: "header file". Do not use "include" for the file itself. English wordings that trigger the check: `header`, `headers`. Do not use: es `cabecera`; it `header`; sv `headerfil`, `rubrik`.
* **Include**: The #include directive and the file it pulls in. The keyword #include itself is never translated. English wordings that trigger the check: `include file`, `include files`, `includes`, `#include`, `open include`, `included file`, `included files`.
* **Message (file)**: A numbered text of a .msg message file (NPC line, player reply, UI message of the game). "Message file" = the .msg file; say "message file", never ".msg file" or "messages file". English wordings that trigger the check: `message`, `messages`. Do not use: de `meldung`; pl `wiadomo`. Not allowed in English: `messages file`, `msg file`, `msg files`.
* **Dialogue**: The conversation between the player and an NPC. (A "dialog" in the sense of a window is a dialog box, not this term.) English wordings that trigger the check: `dialogue`, `dialogues`. Not allowed in English: `dialog node`, `dialog nodes`, `dialog exit node`, `dialog function`.
* **Node**: A node of the dialogue flowchart = an NPC/player dialogue procedure (Node001...). Always "node", not "block", "box" or "step". English wordings that trigger the check: `node`, `nodes`.
* **Flowchart**: The dialogue flowchart window/file (.fcd). A short form ("diagram" in es/it/cs, "schemat" in pl) is accepted when the context is clear. English wordings that trigger the check: `flowchart`, `flowcharts`. Not allowed in English: `nodes flowchart`.
* **Player**: The player character (PC) that answers in a dialogue. Gender-neutral wherever the language allows. English wordings that trigger the check: `player`.
* **Opcode**: An engine/sfall opcode. The word stays "opcode" (not translated) in every language. English wordings that trigger the check: `opcode`, `opcodes`.
* **Register**: Adding a script to scripts.lst (register). Not the Windows registry and not "save". English wordings that trigger the check: `register`, `registered`, `registering`.
* **Template**: A script template (.ssl) or a node code template. English wordings that trigger the check: `template`, `templates`.
* **Archive**: The Fallout game archive (master.dat / critter.dat). es/pt use the same word as for "file" (archivo/arquivo); zh: 档案, never 存档 (that means a saved game). English wordings that trigger the check: `archive`, `archives`. Do not use: zh `存档`.

### Tools

| English | cs | de | es | fr | it | pl | pt | ru | sv | zh |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Compile** | kompilovat | kompilieren | compilar | compiler | compilare | kompilować | compilar | компилировать | kompilera | 编译 |
| **Compiler** | kompilátor | Compiler | compilador | compilateur | compilatore | kompilator | compilador | компилятор | kompilator | 编译器 |
| **Decompile / Decompiler** | dekompilovat | dekompilieren | descompilar | décompiler | decompilare | dekompilować | descompilar | декомпилировать | dekompilera | 反编译 |
| **Preprocess / Preprocessor** | předzpracování / preprocesor | Vorverarbeitung / Präprozessor | preprocesamiento / preprocesador | prétraitement / préprocesseur | preelaborazione / preprocessore | przetwarzanie wstępne / preprocesor | pré-processamento / pré-processador | предобработка / препроцессор | förbearbetning / förprocessor | 预处理 / 预处理器 |
| **Round trip** | zpětná kontrola | Round-Trip | ida y vuelta | aller-retour | andata e ritorno | kompilacja zwrotna | ida e volta | круговая проверка | tur och retur | 往返检查 |
| **Parser** | parser | Parser | analizador | analyseur | parser | parser | analisador | парсер | parser | 解析器 |
| **Native** | nativní | nativ | nativo | natif | nativo | natywny | nativo | нативный | nativ | 原生 |
| **Built-in** | vestavěný | integriert | integrado | intégré | integrato | wbudowany | integrado | встроенный | inbyggd | 内置 |
| **External** | externí | extern | externo | externe | esterno | zewnętrzny | externo | внешний | extern | 外部 |

Usage notes:

* **Compile**: Turning an .ssl script into an .int (the compile verb and its nouns). English wordings that trigger the check: `compile`, `compiled`, `compiling`, `compilation`, `compiles`.
* **Compiler**: The compiler program (sslc, compile.exe). English wordings that trigger the check: `compiler`, `compilers`.
* **Decompile / Decompiler**: Turning an .int back into .ssl (int2ssl or the built-in decompiler). English wordings that trigger the check: `decompile`, `decompiled`, `decompiling`, `decompilation`, `decompiler`, `decompilers`.
* **Preprocess / Preprocessor**: Running the C-style preprocessor (mcpp/wcc/built-in) before the compiler. English wordings that trigger the check: `preprocess`, `preprocessing`, `preprocessed`, `preprocessor`, `preprocessors`. Do not use: it `preprocessing`; sv `förbehandl`.
* **Round trip**: Compile a script and decompile it back to check the result. Written "Round trip" in English. English wordings that trigger the check: `round trip`, `roundtrip`. Not allowed in English: `roundtrip`.
* **Parser**: The script parser that supplies procedures, variables and errors. es/fr/pt translate it (analizador, analyseur, analisador); the other languages keep "parser" (ru: парсер, zh: 解析器). English wordings that trigger the check: `parser`, `parsers`. Do not use: cs `analyzátor`; es `parser`; fr `parser`; it `analizzatore`; pt `parser`; sv `tolk`.
* **Native**: The native parser (parser.dll, x86) as opposed to the built-in C# parser. zh: 原生, not 本机. English wordings that trigger the check: `native`. Do not use: zh `本机`.
* **Built-in**: Shipped inside the editor (built-in parser/decompiler). Not "internal" and not "native". English wordings that trigger the check: `built-in`.
* **External**: A program outside the editor (external editor, external decompiler). English wordings that trigger the check: `external`.

### Editor and interface

| English | cs | de | es | fr | it | pl | pt | ru | sv | zh |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Project** | projekt | Projekt | proyecto | projet | progetto | projekt | projeto | проект | projekt | 项目 |
| **Problem** | problém | Problem | problema | problème | problema | problem | problema | проблема | problem | 问题 |
| **Settings** | nastavení | Einstellungen | configuración | paramètres | impostazioni | ustawienia | configurações | настройки | inställningar | 设置 |
| **Theme** | motiv | Design | tema | thème | tema | motyw | tema | тема | tema | 主题 |
| **Language** | jazyk | Sprache | idioma | langue | lingua | język | idioma | язык | språk | 语言 |
| **Editor** | editor | Editor | editor | éditeur | editor | edytor | editor | редактор | redigerare | 编辑器 |
| **Folder** | složka | Ordner | carpeta | dossier | cartella | folder | pasta | папка | mapp | 文件夹 |
| **Encoding** | kódování | Kodierung | codificación | encodage | codifica | kodowanie | codificação | кодировка | teckenkodning | 编码 |
| **Log** | protokol | Protokoll | registro | journal | registro | dziennik | registro | журнал | logg | 日志 |
| **Reference** | odkaz | Verweis | referencia | référence | riferimento | odwołanie | referência | ссылка | referens | 引用 |
| **Rename** | přejmenovat | umbenennen | cambiar nombre | renommer | rinominare | zmienić nazwę | renomear | переименовать | byta namn | 重命名 |
| **Associate** | přidružit | zuordnen | asociar | associer | associare | skojarzyć | associar | связать | associera | 关联 |
| **Collapse** | sbalit | einklappen | contraer | réduire | comprimere | zwinąć | recolher | свернуть | fälla ihop | 折叠 |
| **Expand** | rozbalit | ausklappen | expandir | développer | espandere | rozwinąć | expandir | развернуть | expandera | 展开 |
| **Comment** | komentář | Kommentar | comentario | commentaire | commento | komentarz | comentário | комментарий | kommentar | 注释 |
| **Indent** | odsazení | Einzug | sangría | retrait | rientro | wcięcie | recuo | отступ | indrag | 缩进 |
| **Caret** | kurzor | Cursor | cursor | curseur | cursore | kursor | cursor | курсор | markör | 光标 |
| **Find / Search** | najít / hledat | suchen | buscar | rechercher | trovare / cercare | znajdź / szukaj | localizar / pesquisar | найти / поиск | söka | 查找 / 搜索 |
| **Replace** | nahradit | ersetzen | reemplazar | remplacer | sostituire | zamienić | substituir | заменить | ersätta | 替换 |
| **Match case** | rozlišovat velikost písmen | Groß-/Kleinschreibung beachten | coincidir mayúsculas y minúsculas | respecter la casse | maiuscole/minuscole | uwzględnij wielkość liter | diferenciar maiúsculas/minúsculas | учитывать регистр | matcha skiftläge | 区分大小写 |
| **Whole word** | pouze celá slova | nur ganze Wörter | solo palabras completas | mot entier uniquement | solo parole intere | tylko całe wyrazy | somente palavras inteiras | только слово целиком | endast hela ord | 全字匹配 |
| **Regular expression** | regulární výraz | regulärer Ausdruck | expresión regular | expression régulière | espressione regolare | wyrażenie regularne | expressão regular | регулярное выражение | reguljärt uttryck | 正则表达式 |
| **Reload** | načíst znovu | neu laden | recargar | recharger | ricaricare | wczytać ponownie | recarregar | перезагрузить | läsa om | 重新加载 |
| **Unsaved** | neuložený | nicht gespeichert | sin guardar | non enregistré | non salvato | niezapisany | não salvo | несохранённый | osparad | 未保存 |
| **Save** | uložit | speichern | guardar | enregistrer | salvare | zapisać | salvar | сохранить | spara | 保存 |
| **Cancel** | zrušit | abbrechen | cancelar | annuler | annullare | anulować | cancelar | отменить | avbryta | 取消 |
| **Delete** | odstranit | löschen | eliminar | supprimer | eliminare | usunąć | excluir | удалить | ta bort | 删除 |
| **Remove** | odebrat | entfernen | quitar | supprimer / retirer | rimuovere | usunąć | remover | удалить | ta bort | 移除 |
| **Undo** | zpět | Rückgängig | deshacer | annuler | annullare | cofnij | desfazer | отменить | ångra | 撤销 |
| **Redo** | znovu | Wiederholen | rehacer | rétablir | ripristina / ripeti | ponów | refazer | повторить | gör om | 重做 |
| **Warning** | upozornění | Warnung | advertencia | avertissement | avviso | ostrzeżenie | aviso | предупреждение | varning | 警告 |
| **Error** | chyba | Fehler | error | erreur | errore | błąd | erro | ошибка | fel | 错误 |

Usage notes:

* **Project**: A project folder with an optional fss-project.json. English wordings that trigger the check: `project`, `projects`.
* **Problem**: An entry of the Errors tab / problem list (error, warning or information). English wordings that trigger the check: `problem`, `problems`.
* **Settings**: The Settings window and the settings file (Visual Studio: Options/Settings). "Options" is reserved for a menu of toggles. English wordings that trigger the check: `settings`, `setting`.
* **Theme**: The light/dark colour theme (Windows/Visual Studio wording). English wordings that trigger the check: `theme`.
* **Language**: The UI or message language. English wordings that trigger the check: `language`.
* **Editor**: The script editor, the message editor, an external editor. English wordings that trigger the check: `editor`, `editors`.
* **Folder**: A directory on disk. Never "directory" or "path" for the folder itself. English wordings that trigger the check: `folder`, `folders`, `subfolder`, `subfolders`.
* **Encoding**: The text encoding / code page used to read and write files. English wordings that trigger the check: `encoding`.
* **Log**: The log panel / build log / parser log / log file (Windows wording "registro", "journal", "Protokoll"). English wordings that trigger the check: `log`. Do not use: it `^log`; pt `^log`.
* **Reference**: Find references: the places that use a symbol. English wordings that trigger the check: `reference`, `references`.
* **Rename**: Rename a symbol, node, file or macro. English wordings that trigger the check: `rename`, `renamed`, `renames`, `renaming`. Do not use: es `renombr`; it `ridenomin`; pt `mudar nome`, `mudar o nome`, `mudança de nome`; sv `döp om`, `omdöp`.
* **Associate**: Message file <-> script association, and the Windows file type association. English wordings that trigger the check: `associate`, `associated`, `association`, `associating`. Do not use: pl `powiąz`; sv `kopp`.
* **Collapse**: Fold an outline region (procedure) of the editor or a node of the flowchart. English wordings that trigger the check: `collapse`, `collapsed`.
* **Expand**: Unfold an outline region. English wordings that trigger the check: `expand`, `expanded`.
* **Comment**: Comment / uncomment code; a comment line of a .msg file. English wordings that trigger the check: `comment`, `comments`, `commented`, `uncomment`.
* **Indent**: Left indentation of code. English wordings that trigger the check: `indent`, `indentation`.
* **Caret**: The text cursor of the editor (not the mouse pointer). English wordings that trigger the check: `caret`. Do not use: de `einfügemarke`.
* **Find / Search**: Find / Search (text, references, files). Same verb family for both in most languages (Visual Studio: Find/Search). English wordings that trigger the check: `find`, `search`, `searches`, `searched`, `searching`.
* **Replace**: Replace text. English wordings that trigger the check: `replace`, `replaced`, `replacement`.
* **Match case**: The case-sensitive search option. English wordings that trigger the check: `match case`.
* **Whole word**: The whole-word search option. English wordings that trigger the check: `whole word`, `whole words`.
* **Regular expression**: The regular-expression search option. English wordings that trigger the check: `regular expression`, `regular expressions`.
* **Reload**: Read a file again from disk (discarding unsaved changes). Not "refresh" and not "update". English wordings that trigger the check: `reload`, `reloads`, `reloading`.
* **Unsaved**: Changes that were not written to disk yet. English wordings that trigger the check: `unsaved`.
* **Save**: Write to disk. fr: enregistrer (also reserved for Save; "register" uses inscrire). English wordings that trigger the check: `save`, `saved`, `saving`.
* **Cancel**: Cancel an operation or a dialog. English wordings that trigger the check: `cancel`, `cancelled`, `canceled`, `cancelling`.
* **Delete**: Permanently delete data (procedure, node, file, line). "Remove" is for taking an entry out of a list. English wordings that trigger the check: `delete`, `deleted`, `deleting`. Do not use: pt `eliminar`.
* **Remove**: Take an entry out of a list or strip characters (not a destructive delete). English wordings that trigger the check: `remove`, `removed`.
* **Undo**: Undo the last edit. English wordings that trigger the check: `undo`.
* **Redo**: Redo the undone edit. English wordings that trigger the check: `redo`.
* **Warning**: A compiler/parser warning (below an error). English wordings that trigger the check: `warning`, `warnings`. Do not use: cs `varován`.
* **Error**: A compiler/parser/tool error. English wordings that trigger the check: `error`, `errors`.

## Excluded in a whole language

* **pt**: `ficheiro` (European Portuguese; the app uses Brazilian Portuguese (arquivo)); `utilizador` (European Portuguese (usuário)); `guardar` (European Portuguese for Save (salvar)); `registo` (European Portuguese (registro)); `ecrã` (European Portuguese (tela)); `tem a certeza` (European Portuguese (tem certeza)); `eliminar` (European Portuguese for Delete (excluir)); `descarreg` (European Portuguese (baixar)); `separador` (European Portuguese for tab (guia)); `a carregar` (European Portuguese gerund (carregando)).
* **zh**: characters `設檔體軟資訊錯誤這個點選視專儲編說閉開顯來為與對應過後無沒時節變數條規則範圍輸輯單頁據請題報錄複製貼畫線鍵滑預覽讀寫連檢壓縮環實際` (Traditional characters; the app uses Simplified Chinese (简体)).
