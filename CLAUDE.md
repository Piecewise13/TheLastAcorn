# The Last Acorn

Unity project. The rules that govern how agents work here live in `.cursor/rules/`
so that Cursor and Claude Code read from one source. They are imported below —
do not restate their contents here, edit the rule file instead.

## Always-on rules

@.cursor/rules/obsidian-vault.mdc
@.cursor/rules/graphify.mdc
@.cursor/rules/unitask-over-coroutines.mdc

## Reading these

- The imports above are the authoritative text. If something in this file ever
  contradicts a rule file, the rule file wins.
- The Obsidian vault rule requires reading `98-Agent/Preferences.md` in the vault
  before writing or editing any vault note. That read is not optional and the
  imported rule text is not a substitute for it.
- Pass the relevant rule text into every subagent prompt. Subagents do not inherit
  this file.
