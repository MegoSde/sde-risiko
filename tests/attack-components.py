"""Attack Component API, direction, reuse, SQLite persistence and legacy migration."""
import contextlib
import json
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'bin/Debug/net10.0/AlpacaMedic.dll'
EF = os.environ.get('EF_TOOL', '/tmp/alpacamedic-ef/dotnet-ef')

@contextlib.contextmanager
def server(database):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen(['dotnet', str(DLL), f'--Urls=http://127.0.0.1:{port}', f'--DatabasePath={database}', '--Logging:LogLevel:Default=Warning'], cwd=ROOT, stdout=log, stderr=log)
        def request(path, method='GET', data=None, status=200):
            req = urllib.request.Request(f'http://127.0.0.1:{port}' + path, method=method, data=None if data is None else json.dumps(data).encode(), headers={'Content-Type': 'application/json'})
            try: response = urllib.request.urlopen(req, timeout=10)
            except urllib.error.HTTPError as error: response = error
            raw = response.read(); assert response.status == status, (path, response.status, raw.decode())
            return json.loads(raw) if raw and response.headers.get_content_type() == 'application/json' else raw.decode()
        try:
            for _ in range(200):
                try: request('/api/attack-components'); break
                except OSError:
                    if process.poll() is not None:
                        log.seek(0); raise AssertionError(log.read().decode())
                    time.sleep(.1)
            else: raise AssertionError('Server startup timeout')
            yield request
        finally:
            process.terminate(); process.wait(timeout=10)

with tempfile.TemporaryDirectory(prefix='alpacamedic-components-', dir='/tmp') as directory:
    database = Path(directory) / 'components.db'
    with server(database) as request:
        request('/system-analysis', status=404)
        request('/js/system-analysis.js', status=404)
        request('/api/system-access', status=404); request('/api/system-threats', status=404)
        html = request('/threat-analysis'); assert 'Systemanalyse' not in html and 'start-component-form' in html
        technique = request('/api/attack-techniques?search=T1190')[0]
        valid = request('/api/attack-techniques?search=T1078')[0]
        def asset(name): return request('/api/system-assets', 'POST', {'name': name, 'assetType': 'Host'}, 201)
        a, b, target, unreachable = [asset(name) for name in ['WEB test', 'Portal test', 'DB test', 'Unreachable test']]
        assert all('blastRadius' not in s for s in request('/api/system-assets'))
        assessment = {'attackTechniqueId': technique['id'], 'likelihood': 3, 'comment': 'Start'}
        request('/api/attack-components', 'POST', dict(assessment, systemAssetId=target['id']), 400)
        for s in [a,b]: request('/api/attack-surface', 'POST', {'systemAssetId': s['id'], 'isExposed': True, 'exposureType': 'Internet'}, 201)
        ca = request('/api/attack-components', 'POST', dict(assessment, systemAssetId=a['id']), 201)['component']
        cb = request('/api/attack-components', 'POST', dict(assessment, systemAssetId=b['id']), 201)['component']
        reused = request('/api/attack-components', 'POST', dict(assessment, systemAssetId=a['id'], likelihood=5, comment='Do not overwrite'))
        assert reused['component'] == ca and not reused['componentCreated']
        ra = request('/api/system-asset-relations', 'POST', {'sourceSystemAssetId': a['id'], 'targetSystemAssetId': target['id'], 'relationType': 'ConnectsTo'}, 201)
        rb = request('/api/system-asset-relations', 'POST', {'sourceSystemAssetId': b['id'], 'targetSystemAssetId': target['id'], 'relationType': 'Uses'}, 201)
        assert [n['asset']['id'] for n in request(f"/api/attack-components/{ca['id']}/next-assets")] == [target['id']]
        body = {'systemAssetRelationId': ra['id'], 'attackTechniqueId': valid['id'], 'likelihood': 3, 'comment': 'Shared DB assessment'}
        first = request(f"/api/attack-components/{ca['id']}/next", 'POST', body, 201); ct = first['component']
        second = request(f"/api/attack-components/{cb['id']}/next", 'POST', dict(body, systemAssetRelationId=rb['id'], likelihood=5, comment='Keep shared assessment'), 201)
        assert second['component'] == ct and not second['componentCreated'] and second['connectionCreated']
        again = request(f"/api/attack-components/{ca['id']}/next", 'POST', body)
        assert not again['componentCreated'] and not again['connectionCreated']
        inbound = request(f"/api/attack-connections?toAttackComponentId={ct['id']}")
        assert {c['fromAttackComponentId'] for c in inbound} == {ca['id'],cb['id']}
        assert len([c for c in request('/api/attack-components') if c['systemAssetId'] == target['id'] and c['attackTechniqueId'] == valid['id']]) == 1
        direct = request('/api/attack-connections', 'POST', inbound[0]); assert direct == inbound[0]
        for invalid in [dict(body, systemAssetRelationId=rb['id']), dict(body, systemAssetRelationId=999999), dict(body, attackTechniqueId=999999), dict(body, likelihood=0), dict(body, likelihood=6), dict(body, comment=None)]:
            request(f"/api/attack-components/{ca['id']}/next", 'POST', invalid, 400)
        request('/api/attack-connections', 'POST', {'fromAttackComponentId': ct['id'], 'toAttackComponentId': ca['id'], 'systemAssetRelationId': ra['id']}, 400)
        request(f"/api/system-asset-relations/{ra['id']}", 'PUT', dict(ra, targetSystemAssetId=unreachable['id']), 400)
        changed = request(f"/api/attack-components/{ct['id']}", 'PUT', {'likelihood': 4, 'comment': 'Single updated assessment'})
        assert changed['systemAssetId'] == target['id'] and changed['attackTechniqueId'] == valid['id']
        request('/api/attack-components/999999', 'PUT', {'likelihood': 3}, 404)
        scenarios = request('/api/risks')
        assert {r['id'] for r in scenarios} == {ca['id'], cb['id']}
        assert all(1 <= r['riskScore'] <= 25 for r in scenarios)
        before_components = request('/api/attack-components'); before_connections = request('/api/attack-connections')
        assert request('/api/dashboard')['attackFindings'] == len(before_components)
        for page in ['dashboard','business-analysis','asset-model','attack-surface','threat-analysis','controls','risk-analysis','risk-matrix','cis-priorities','report']: request('/' + page)
    with server(database) as request:
        assert request('/api/attack-components') == before_components
        assert request('/api/attack-connections') == before_connections
        request(f"/api/attack-connections/{inbound[0]['id']}", 'DELETE', status=204)
        assert len(request(f"/api/attack-connections?toAttackComponentId={ct['id']}")) == 1
        assert len(request('/api/attack-components')) == 3
        request(f"/api/system-asset-relations/{rb['id']}", 'DELETE', status=204)
        assert request('/api/attack-connections') == []
        request(f"/api/attack-components/{ct['id']}", 'DELETE', status=204)
        assert len(request('/api/attack-components')) == 2
    with sqlite3.connect(database) as db:
        assert db.execute('PRAGMA foreign_key_check').fetchall() == []
        tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        assert not {'SystemAccess','SystemThreats','ThreatControls'} & tables
        assert not {'BlastRadius','BlastRadiusReason'} & {r[1] for r in db.execute('PRAGMA table_info(SystemAssets)')}
    legacy = Path(directory) / 'legacy.db'
    result = subprocess.run([EF,'database','update','AttackCatalog','--no-build','--connection',f'Data Source={legacy}'], cwd=ROOT, env=dict(os.environ, DatabasePath=str(legacy)), capture_output=True,text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    with sqlite3.connect(legacy) as db:
        db.executescript("""
            INSERT INTO SystemAssets (Id,Name,Description,AssetType,BlastRadius,BlastRadiusReason) VALUES (91,'Legacy','Keep','Host','3','Remove manual assessment');
            INSERT INTO AttackTechniques (Id,TechniqueId,Name,Tactic,Description,MitreUrl,IsSubTechnique,ParentTechniqueId,Platforms) VALUES (1,'T1190','Legacy technique','Initial Access','Keep','https://attack.mitre.org/techniques/T1190/',0,NULL,'');
            INSERT INTO CisControls VALUES (7,7,'CIS test','Keep');
            INSERT INTO SystemThreats VALUES (98,91,1,'First scenario',2);
            INSERT INTO SystemThreats VALUES (99,91,1,'Second scenario',4);
            INSERT INTO ThreatControls (SystemThreatId,CisControlId,Comment,IsRecommended) VALUES (98,7,'First control note',0);
            INSERT INTO ThreatControls (SystemThreatId,CisControlId,Comment,IsRecommended) VALUES (99,7,'Second control note',1);
            """)
        db.executemany('INSERT OR IGNORE INTO CisControls VALUES (?,?,?,?)', [(n,n,'CIS '+str(n),'Fixture') for n in range(1,19)])
    with server(legacy) as request:
        migrated = request('/api/attack-components'); assert len(migrated) == 1
        assert migrated[0]['id'] == 98 and migrated[0]['likelihood'] == 4
        assert 'First scenario' in migrated[0]['comment'] and 'Second scenario' in migrated[0]['comment']
        controls = request('/api/component-controls'); assert len(controls) == 1 and controls[0]['isRecommended']
        assert controls[0]['attackComponentId'] == 98 and 'First control note' in controls[0]['comment'] and 'Second control note' in controls[0]['comment']
        assert request('/api/attack-connections') == []
    print('PASS: removed System Analysis/manual Blast Radius, surface-only starts, directed next steps, component/connection reuse, multiple inbound connections, validation, deletion/cascades, derived scenarios, restart persistence and legacy assessment/Controls consolidation.')
