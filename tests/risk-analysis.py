"""Derived risk calculations, directed complete paths, cycles, deduplication and all matrix scores."""
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
EF = os.environ.get('EF_TOOL','/tmp/alpacamedic-ef/dotnet-ef')

@contextlib.contextmanager
def server(database):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1',0)); port=sock.getsockname()[1]
    with tempfile.TemporaryFile() as log:
        process=subprocess.Popen(['dotnet',str(DLL),f'--Urls=http://127.0.0.1:{port}',f'--DatabasePath={database}','--Logging:LogLevel:Default=Warning'],cwd=ROOT,stdout=log,stderr=log)
        def request(path,method='GET',body=None,status=200):
            req=urllib.request.Request(f'http://127.0.0.1:{port}'+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
            try:r=urllib.request.urlopen(req,timeout=10)
            except urllib.error.HTTPError as error:r=error
            raw=r.read();assert r.status==status,(path,r.status,raw.decode())
            return json.loads(raw) if raw and r.headers.get_content_type()=='application/json' else raw.decode()
        try:
            for _ in range(200):
                try:request('/api/risks');break
                except OSError:
                    if process.poll() is not None:
                        log.seek(0);raise AssertionError(log.read().decode())
                    time.sleep(.1)
            else:raise AssertionError('Server startup timed out')
            yield request
        finally:process.terminate();process.wait(timeout=10)

with tempfile.TemporaryDirectory(prefix='alpacamedic-risks-',dir='/tmp') as directory:
    database=Path(directory)/'risk.db'
    result=subprocess.run([EF,'database','update','AttackComponents','--no-build','--connection',f'Data Source={database}'],cwd=ROOT,env=dict(os.environ,DatabasePath=str(database)),capture_output=True,text=True)
    assert result.returncode==0,result.stdout+result.stderr
    # A historical Attack Surface component must become an initial scenario at migration.
    with sqlite3.connect(database) as db:
        db.executemany('INSERT INTO CisControls VALUES (?,?,?,?)',[(n,n,'CIS '+str(n),'Fixture') for n in range(1,19)])
        db.executemany('INSERT INTO AttackTechniques (Id,TechniqueId,Name,Tactic,Description,MitreUrl,IsSubTechnique,Platforms) VALUES (?,?,?,?,?,?,?,?)',[(n,tid,name,'Initial Access','Fixture','https://attack.mitre.org/techniques/'+tid+'/',0,'Linux') for n,tid,name in [(1,'T1190','Exploit'),(2,'T1078','Valid Accounts'),(3,'T1059','Interpreter'),(4,'T1005','Data')]])
        db.execute("INSERT INTO SystemAssets (Id,Name,Description,AssetType) VALUES (91,'Historical entry','Fixture','Host')")
        db.execute("INSERT INTO AttackSurfaceEntries VALUES (91,91,1,'Internet','Fixture')")
        db.execute("INSERT INTO AttackComponents (Id,SystemAssetId,AttackTechniqueId,Likelihood,Comment) VALUES (91,91,1,4,'Historical start')")
    with server(database) as request:
        assert request('/api/attack-components')[0]['isInitial']
        legacy=request('/api/risks');assert len(legacy)==1 and legacy[0]['id']==91
        assert legacy[0]['scenarioLikelihood']==4 and legacy[0]['consequenceIndex']==1 and legacy[0]['maximumConsequence']==0
        request('/api/attack-surface/91','PUT',{'systemAssetId':91,'isExposed':False,'exposureType':'Internet'})
        def process(name,cia):return request('/api/business-processes','POST',dict(name=name,confidentiality=cia,integrity=cia,availability=cia),201)
        clinical,research,small=process('Clinical',3),process('Research',2),process('Small',1)
        assert (clinical['consequence'],research['consequence'],small['consequence'])==(5,3,1)
        def business(name,processes):
            a=request('/api/business-assets','POST',{'name':name,'businessProcessId':processes[0]['id']},201)
            for p in processes[1:]:request('/api/business-assets','POST',{'name':name,'businessProcessId':p['id']},201)
            return a
        shared=business('Shared Clinical Data',[clinical,research]);patient=business('Patient Data',[clinical]);identity=business('Identity',[research]);outside=business('Unreached Asset',[research,small])
        def system(name):return request('/api/system-assets','POST',{'name':name,'assetType':'Host'},201)
        a,b,c,d,z=[system(n) for n in ['WEB','Portal','DB','ClinicalDataSystem','Outside']]
        def expose(s):return request('/api/attack-surface','POST',{'systemAssetId':s['id'],'isExposed':True,'exposureType':'Internet'},201)
        ea,eb,ez=expose(a),expose(b),expose(z)
        def start(s,tid,p):return request('/api/attack-components','POST',{'systemAssetId':s['id'],'attackTechniqueId':tid,'likelihood':p},201)['component']
        ca=start(a,1,4);solo=start(a,3,5);isolated=start(z,2,1)
        def relation(source,target):return request('/api/system-asset-relations','POST',{'sourceSystemAssetId':source['id'],'targetSystemAssetId':target['id'],'relationType':'ConnectsTo'},201)
        def next_component(source,edge,tid,p):return request(f"/api/attack-components/{source['id']}/next",'POST',{'systemAssetRelationId':edge['id'],'attackTechniqueId':tid,'likelihood':p},201)['component']
        rac=relation(a,c);cab=relation(a,b);rcd=relation(c,d);rbd=relation(b,d);rbc=relation(b,c);rca=relation(c,a)
        relation(d,z) # Asset Model alone cannot extend the attack graph.
        cc=next_component(ca,rac,2,3);cb=next_component(ca,cab,3,2);cd=next_component(cc,rcd,4,4)
        next_component(cb,rbd,4,5);next_component(cb,rbc,2,5);next_component(cc,rca,1,5)
        assert not cb['isInitial'],'A downstream component on an exposed asset is not an automatic scenario'
        def mapping(asset,sys,kind='StoredIn'):return request('/api/business-asset-relations','POST',{'businessAssetId':asset['id'],'systemAssetId':sys['id'],'relationType':kind},201)
        mapping(shared,c);mapping(shared,d);mapping(shared,c,'ProcessedBy');mapping(patient,a);mapping(identity,b);mapping(outside,z)
        def risk(cid):return request(f"/api/risks/{cid}")
        r=risk(ca['id'])
        assert {row['id'] for row in request('/api/risks')}=={ca['id'],solo['id'],isolated['id']}
        assert len(r['paths'])==5 and sorted(p['pathLikelihood'] for p in r['paths'])==[2,2,2,3,3]
        assert len([p for p in r['paths'] if p['cycleToComponentId'] is not None])==2
        assert all(len(p['components'])==len({c['id'] for c in p['components']}) for p in r['paths'])
        assert {c['id'] for path in r['paths'] for c in path['components']}=={ca['id'],cb['id'],cc['id'],cd['id']}
        assert all(p['pathLikelihood']==min(c['likelihood'] for c in p['components']) for p in r['paths'])
        assert (r['accumulatedConsequence'],r['maximumConsequence'],r['impactRatio'],r['consequenceIndex'],r['scenarioLikelihood'],r['riskScore'],r['riskLevel'])==(16,20,.8,4,3,12,'High'),r
        reached=r['reachableBusinessAssets'];assert len(reached)==3 and len({a['id'] for a in reached})==3
        shared_row=next(asset for asset in reached if asset['id']==shared['id']);assert shared_row['consequence']==8 and len(shared_row['businessProcesses'])==2 and len(shared_row['reachedViaSystemAssetIds'])==2
        single=risk(solo['id']);assert len(single['paths'])==1 and single['scenarioLikelihood']==5 and single['riskScore']==10 and single['riskLevel']=='Medium'
        assert risk(isolated['id'])['riskScore']==1
        request(f"/api/attack-components/{cc['id']}",'PUT',{'likelihood':5});assert risk(ca['id'])['scenarioLikelihood']==4
        request(f"/api/attack-components/{ca['id']}",'PUT',{'likelihood':5})
        request(f"/api/attack-components/{cd['id']}",'PUT',{'likelihood':5});assert risk(ca['id'])['riskScore']==20 and risk(ca['id'])['riskLevel']=='Critical'
        mapping(outside,d);assert risk(ca['id'])['accumulatedConsequence']==20 and risk(ca['id'])['consequenceIndex']==5 and risk(ca['id'])['riskScore']==25
        request(f"/api/attack-surface/{ea['id']}",'PUT',dict(ea,isExposed=False));assert {r['id'] for r in request('/api/risks')}=={isolated['id']}
        request(f"/api/attack-surface/{ea['id']}",'PUT',dict(ea,isExposed=True));assert len(request('/api/risks'))==3
        request(f"/api/business-processes/{clinical['id']}",'PUT',dict(clinical,confidentiality=2,integrity=2,availability=2))
        changed=risk(ca['id']);assert changed['maximumConsequence']==16 and changed['accumulatedConsequence']==16
        assert next(a for a in changed['reachableBusinessAssets'] if a['id']==shared['id'])['consequence']==6
        saved=request('/api/risks');request('/api/risks/999999',status=404)
    with server(database) as request:
        assert request('/api/risks')==saved
        # Isolate score/band tests from previous scenarios and Business Assets.
        for e in request('/api/attack-surface'):request(f"/api/attack-surface/{e['id']}",'PUT',dict(e,isExposed=False))
        for a in request('/api/business-assets'):request(f"/api/business-assets/{a['id']}",'DELETE',status=204)
        p1=request('/api/business-processes','POST',{'name':'Band P1','confidentiality':1,'integrity':1,'availability':1},201)
        p2=request('/api/business-processes','POST',{'name':'Band P2','confidentiality':1,'integrity':1,'availability':1},201)
        extra=request('/api/business-processes','POST',{'name':'Band Extra','confidentiality':2,'integrity':1,'availability':1},201);assert extra['consequence']==1.5
        unit=[]
        for i in range(5):
            a=request('/api/business-assets','POST',{'name':'Band Asset '+str(i),'businessProcessId':p1['id']},201)
            request('/api/business-assets','POST',{'name':a['name'],'businessProcessId':p2['id']},201);unit.append(a)
        root=request('/api/system-assets','POST',{'name':'Band root','assetType':'Host'},201)
        request('/api/attack-surface','POST',{'systemAssetId':root['id'],'isExposed':True,'exposureType':'Internet'},201)
        component=request('/api/attack-components','POST',{'systemAssetId':root['id'],'attackTechniqueId':1,'likelihood':1},201)['component']
        for index in range(1,6):
            request('/api/business-asset-relations','POST',{'businessAssetId':unit[index-1]['id'],'systemAssetId':root['id'],'relationType':'StoredIn'},201)
            for likelihood in range(1,6):
                request(f"/api/attack-components/{component['id']}",'PUT',{'likelihood':likelihood})
                row=request('/api/risks')[0];score=index*likelihood
                assert row['consequenceIndex']==index and row['impactRatio']==index/5 and row['maximumConsequence']==10 and row['riskScore']==score
                assert row['riskLevel']==('Low' if score<=5 else 'Medium' if score<12 else 'High' if score<20 else 'Critical')
                assert row['paths'][0]['pathLikelihood']==likelihood and len(row['paths'])==1
            if index<5:
                request('/api/business-assets','POST',{'name':unit[0]['name'],'businessProcessId':extra['id']},201)
                row=request('/api/risks')[0];assert row['consequenceIndex']==index+1 and row['accumulatedConsequence']==index*2+1.5 and row['maximumConsequence']==11.5
                request(f"/api/business-assets/{unit[0]['id']}/processes/{extra['id']}",'DELETE',status=204)
        levels=request('/api/risk-levels');assert levels['lowMax']==5 and levels['highMin']==12 and levels['criticalMin']==20
        for a in unit:request(f"/api/business-assets/{a['id']}",'DELETE',status=204)
        row=request('/api/risks')[0];assert row['maximumConsequence']==0 and row['accumulatedConsequence']==0 and row['impactRatio']==0 and row['consequenceIndex']==1 and row['riskScore']==5
        orphan=request('/api/business-assets','POST',{'name':'Orphan'},201)
        request('/api/business-asset-relations','POST',{'businessAssetId':orphan['id'],'systemAssetId':root['id'],'relationType':'StoredIn'},201)
        row=request('/api/risks')[0];assert row['maximumConsequence']==0 and len(row['reachableBusinessAssets'])==1 and row['reachableBusinessAssets'][0]['consequence']==0
        assert all('blastRadius' not in a for a in request('/api/system-assets'))
    with sqlite3.connect(database) as db:
        assert db.execute('PRAGMA foreign_key_check').fetchall()==[]
        assert not any('RiskScore' in row[1] or 'BlastRadius' in row[1] for table in ['AttackComponents','SystemAssets'] for row in db.execute(f'PRAGMA table_info({table})'))
    print('PASS: initial scenario migration/selection, shared nodes and cycles, complete directed paths, MIN/MAX likelihood, Business Asset and process deduplication, summed process consequences, current-model updates, all 25 matrix scores/levels, exact/above impact boundaries, zero/orphan assets and restart persistence; no stored risk or Blast Radius.')
