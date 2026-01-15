Dataset은 “영상 전체 컨테이너”다.
안에 뭐가 들어있냐면:

Dataset
 ├─ RasterXSize  (가로 픽셀 수)
 ├─ RasterYSize  (세로 픽셀 수)
 ├─ RasterCount  (밴드 개수)
 ├─ GeoTransform (좌표 변환 정보)
 ├─ Projection   (좌표계)
 └─ Band 1
     Band 2
     Band 3

👉 픽셀 값은 Dataset 안에 직접 없다
👉 항상 Band를 통해서만 접근한다

Dataset ds
 : “이 변수는 영상 전체를 대표하는 객체다”

Gdal.Open(...)
 -“파일 경로를 GDAL에게 넘긴다”
 - 성공하면 Dataset 생성
 - 실패하면 null

Access.GA_ReadOnly
 - 읽기 전용
 - 실무에서 기본값