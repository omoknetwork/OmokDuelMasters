using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BoardManager))]
public class BoardManagerEditor : Editor
{
    private void OnSceneGUI()
    {
        // 대상이 될 BoardManager 컴포넌트를 가져옵니다.
        BoardManager boardManager = (BoardManager)target;

        // 씬 뷰에서 핸들(화살표)을 그리기 전, 현재 회전값을 적용합니다.
        Quaternion rotation = Quaternion.Euler(boardManager.boardRotation);

        // 씬 뷰에 이동 가능한 포지션 핸들(화살표)을 그립니다.
        EditorGUI.BeginChangeCheck();
        
        // 크기를 적절하게 조절하여 눈에 잘 띄게 만듭니다.
        float handleSize = HandleUtility.GetHandleSize(boardManager.boardOrigin) * 0.5f;
        Vector3 newOrigin = Handles.PositionHandle(boardManager.boardOrigin, rotation);

        // 핸들을 드래그해서 값이 변경되었다면
        if (EditorGUI.EndChangeCheck())
        {
            // 변경된 값을 BoardManager의 boardOrigin 변수에 덮어씌웁니다.
            Undo.RecordObject(boardManager, "위치 이동: Board Origin");
            boardManager.boardOrigin = newOrigin;

            // 추가: 변경 사항이 저장되도록 씬에 더티 플래그를 넘깁니다.
            EditorUtility.SetDirty(boardManager);
        }
    }
}