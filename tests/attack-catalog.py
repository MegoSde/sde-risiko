"""Local STIX import, SQLite search and startup skip checks on disposable databases."""
import contextlib
import json
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'bin/Debug/net10.0/AlpacaMedic.dll'


@contextlib.contextmanager
def server(database, content_root, log):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    with log.open('w') as output:
        process = subprocess.Popen(['dotnet', str(DLL), f'--Urls={base}', f'--DatabasePath={database}',
                                    f'--ContentRoot={content_root}', '--Logging:LogLevel:Default=Information'],
                                   cwd=ROOT, stdout=output, stderr=output)
        try:
            def search(term=''):
                with urllib.request.urlopen(base + '/api/attack-techniques?search=' + urllib.parse.quote(term), timeout=5) as response:
                    return json.load(response)
            for _ in range(250):
                try:
                    search('T1190')
                    break
                except (OSError, TimeoutError):
                    if process.poll() is not None:
                        raise AssertionError(log.read_text())
                    time.sleep(0.1)
            else:
                raise AssertionError('Import server failed to start: ' + log.read_text())
            yield search
        finally:
            process.terminate()
            process.wait(timeout=10)


with tempfile.TemporaryDirectory(prefix='alpacamedic-catalog-', dir='/tmp') as directory:
    work = Path(directory)
    with (ROOT / 'Data/Seed/enterprise-attack.json').open() as file:
        objects = json.load(file)['objects']
    expected = {}
    excluded = set()
    for obj in objects:
        if obj.get('type') != 'attack-pattern':
            continue
        refs = [r for r in obj.get('external_references', []) if r.get('source_name') == 'mitre-attack' and r.get('external_id', '').startswith('T')]
        if not refs:
            continue
        technique_id = refs[0]['external_id']
        if obj.get('revoked') or obj.get('x_mitre_deprecated'):
            excluded.add(technique_id)
        elif 'x_mitre_domains' not in obj or 'enterprise-attack' in obj['x_mitre_domains']:
            expected[technique_id] = obj
    database = work / 'full.db'
    with server(database, ROOT, work / 'first.log') as search:
        assert len(search()) <= 25
        t1190 = search('t1190')
        assert t1190[0]['techniqueId'] == 'T1190'
        assert t1190[0]['name'] == 'Exploit Public-Facing Application'
        assert 'Initial Access' in t1190[0]['tactic']
        assert search('PHISHING') and all('phishing' in t['name'].lower() for t in search('PHISHING'))
        assert search('initial-access') and len(search('Initial Access')) <= 25
        with sqlite3.connect(database) as db:
            actual = dict(db.execute('SELECT TechniqueId, Name FROM AttackTechniques'))
            assert set(actual) == set(expected), (len(actual), len(expected))
            assert not (set(actual) & (excluded - set(expected)))
            tactic_count = db.execute('SELECT COUNT(*) FROM AttackTactics').fetchone()[0]
            assert tactic_count == len([o for o in objects if o['type'] == 'x-mitre-tactic' and not o.get('revoked') and not o.get('x_mitre_deprecated')])
            assert db.execute('SELECT COUNT(*) FROM AttackTechniqueTactics').fetchone()[0] > len(actual)
            sub = db.execute("SELECT IsSubTechnique, ParentTechniqueId, Platforms FROM AttackTechniques WHERE TechniqueId='T1566.001'").fetchone()
            assert sub[0:2] == (1, 'T1566') and sub[2]
            assert db.execute('PRAGMA foreign_key_check').fetchall() == []
        # Search individual excluded IDs, not just the first 25 results.
        for technique_id in sorted(excluded - set(expected)):
            assert all(t['techniqueId'] != technique_id for t in search(technique_id))
    assert f'Imported {len(expected)} techniques and {tactic_count} tactics' in (work / 'first.log').read_text()
    # No STIX file at this content root: a successful restart proves it is not parsed again.
    with server(database, work, work / 'second.log') as search:
        assert search('T1190')[0]['techniqueId'] == 'T1190'
        assert search('phishing')
    assert f'catalog already imported: {len(expected)} techniques' in (work / 'second.log').read_text()
    assert 'Importing MITRE' not in (work / 'second.log').read_text()
    fixture_root = work / 'fixture'
    fixture_path = fixture_root / 'Data/Seed/enterprise-attack.json'
    fixture_path.parent.mkdir(parents=True)
    def technique(tid, **extra):
        return {'type': 'attack-pattern', 'name': tid + ' Test', 'description': 'Fixture',
                'external_references': [{'source_name': 'other', 'external_id': 'WRONG'}, {'source_name': 'mitre-attack', 'external_id': tid, 'url': 'https://attack.mitre.org/techniques/' + tid + '/'}],
                'kill_chain_phases': [{'kill_chain_name': 'mitre-attack', 'phase_name': 'initial-access'}, {'kill_chain_name': 'mitre-attack', 'phase_name': 'execution'}],
                'x_mitre_platforms': ['Linux', 'Windows'], **extra}
    fixture_path.write_text(json.dumps({'objects': [
        {'type': 'x-mitre-tactic', 'x_mitre_shortname': 'initial-access', 'name': 'Initial Access'},
        {'type': 'x-mitre-tactic', 'x_mitre_shortname': 'execution', 'name': 'Execution'},
        technique('T1190'), technique('T1190'), technique('T1190.001', x_mitre_is_subtechnique=True),
        technique('T9991', revoked=True), technique('T9992', x_mitre_deprecated=True),
        technique('T9993', x_mitre_domains=['mobile-attack']), {'type': 'malware', 'name': 'Ignore'}]}))
    fixture_db = work / 'fixture.db'
    with server(fixture_db, fixture_root, work / 'fixture.log') as search:
        rows = search()
        assert {t['techniqueId'] for t in rows} == {'T1190', 'T1190.001'}
        assert all(t['platforms'] == 'Linux;Windows' for t in rows)
        assert len(search('execution')) == 2
        with sqlite3.connect(fixture_db) as db:
            assert db.execute('SELECT COUNT(*) FROM AttackTechniqueTactics').fetchone()[0] == 4
    print(f'PASS: local full catalog imports {len(expected)} active techniques / {tactic_count} tactics; subsequent startup without STIX file skips import; ID/name/tactic search is case-insensitive and limited to 25; revoked/deprecated/non-Enterprise objects excluded; subtechniques, platforms, multiple tactics and deduplication persist.')
